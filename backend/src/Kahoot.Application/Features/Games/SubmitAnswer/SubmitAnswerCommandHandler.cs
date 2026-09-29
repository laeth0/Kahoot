namespace Kahoot.Application.Features.Games.SubmitAnswer;

using System.Data.Common;
using Kahoot.Application.Common.Exceptions;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Games.Models;
using Kahoot.Application.Features.Games.Scoring;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

public sealed class SubmitAnswerCommandHandler : ICommandHandler<SubmitAnswerCommand, SubmitAnswerResponse>
{
    private readonly IAppDbContext _dbContext;
    private readonly IAnswerRateLimiter _rateLimiter;
    private readonly IGameNotificationService _notificationService;
    private readonly ILogger<SubmitAnswerCommandHandler> _logger;

    public SubmitAnswerCommandHandler(
        IAppDbContext dbContext,
        IAnswerRateLimiter rateLimiter,
        IGameNotificationService notificationService,
        ILogger<SubmitAnswerCommandHandler> logger)
    {
        _dbContext = dbContext;
        _rateLimiter = rateLimiter;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<Result<SubmitAnswerResponse>> Handle(
        SubmitAnswerCommand request,
        CancellationToken cancellationToken)
    {
        // Bind the attempt budget to the server's question, never a client-supplied ID.
        Guid activeQuestionId = await (
            from game in _dbContext.Games.AsNoTracking()
            join question in _dbContext.GameQuestionSnapshots.AsNoTracking()
                on new { GameId = game.Id, OrderIndex = game.CurrentQuestionIndex }
                equals new { GameId = question.GameId, OrderIndex = (int?)question.OrderIndex }
            where game.Id == request.GameId
            select question.Id).FirstOrDefaultAsync(cancellationToken);
        Guid rateQuestionId = activeQuestionId == Guid.Empty ? request.GameId : activeQuestionId;
        bool isRateLimited = await _rateLimiter.IsRateLimitedAsync(
            request.ConnectionId, request.GameId, request.ParticipantId,
            rateQuestionId, cancellationToken);
        if (request.ChoiceIds is null || request.ChoiceIds.Count == 0 ||
            request.ChoiceIds.Count > 6 || request.ChoiceIds.Contains(Guid.Empty))
        {
            return Result.Failure<SubmitAnswerResponse>(GameErrors.InvalidChoices);
        }

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await ExecuteSubmissionAttemptAsync(request, isRateLimited, cancellationToken);
            }
            catch (Exception exception) when (
                attempt < 3 && !cancellationToken.IsCancellationRequested &&
                (exception.GetBaseException() is DbException { SqlState: "40P01" or "55P03" } ||
                 exception is UniqueConstraintViolationException
                 {
                     ConstraintName: "ux_answer_submissions_once_per_question"
                 }))
            {
                // EF retains Added and Modified entities after a failed transaction.
                _dbContext.ClearTrackedChanges();
                _logger.LogWarning(exception,
                    "Answer transaction rolled back; retrying. Attempt={Attempt} GameId={GameId}",
                    attempt, request.GameId);
                await Task.Delay(50 * attempt, cancellationToken);
            }
        }
    }

    private async Task<Result<SubmitAnswerResponse>> ExecuteSubmissionAttemptAsync(
        SubmitAnswerCommand request,
        bool isRateLimited,
        CancellationToken cancellationToken)
    {
        QuestionEndedEvent? endedEvent = null;
        List<PersonalQuestionResultEvent>? personalResults = null;
        Guid hostAccountId = Guid.Empty;
        long stateVersion = 0;

        await using (IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            // The host row comes first in the same lock order as host commands and suspension.
            await _dbContext.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '3s'", cancellationToken);
            List<User> hosts = await _dbContext.Users
                .FromSqlInterpolated($"""
                    SELECT u.* FROM users AS u
                    JOIN games AS g ON g.host_account_id = u.id
                    WHERE g.id = {request.GameId}
                    FOR SHARE OF u
                    """)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            User? host = hosts.Count == 0 ? null : hosts[0];
            if (host is null)
            {
                return Result.Failure<SubmitAnswerResponse>(GameErrors.NotFound);
            }
            if (host.Role != UserRole.Host || host.Status != UserStatus.Active)
            {
                return Result.Failure<SubmitAnswerResponse>(GameErrors.Unavailable);
            }

            // Shared game locks let different participants score concurrently while excluding host transitions.
            List<Game> games = await _dbContext.Games
                .FromSqlInterpolated($"""
                    SELECT * FROM games
                    WHERE id = {request.GameId} AND host_account_id = {host.Id}
                    FOR SHARE
                    """)
                .ToListAsync(cancellationToken);
            Game? game = games.Count == 0 ? null : games[0];
            if (game is null)
            {
                return Result.Failure<SubmitAnswerResponse>(GameErrors.NotFound);
            }

            hostAccountId = game.HostAccountId;
            Participant? participant = await _dbContext.Participants
                .SingleOrDefaultAsync(p => p.Id == request.ParticipantId &&
                                           p.GameId == game.Id &&
                                           p.HostAccountId == hostAccountId, cancellationToken);
            if (participant is null)
            {
                return Result.Failure<SubmitAnswerResponse>(GameErrors.InvalidSessionToken);
            }

            if (participant.IsRemoved)
            {
                return Result.Failure<SubmitAnswerResponse>(GameErrors.ParticipantRemoved);
            }

            // Recheck the REST token under the same game lock that protects host revocation.
            if (request.SessionTokenHash is not null)
            {
                bool tokenIsCurrent = await _dbContext.ParticipantSessionTokens.AsNoTracking().AnyAsync(
                    token => token.GameId == game.Id && token.ParticipantId == participant.Id &&
                             token.TokenHash == request.SessionTokenHash && token.RevokedAt == null,
                    cancellationToken);
                if (!tokenIsCurrent)
                {
                    return Result.Failure<SubmitAnswerResponse>(GameErrors.InvalidSessionToken);
                }
            }
            else if (request.ConnectionGeneration != participant.ConnectionGeneration)
            {
                return Result.Failure<SubmitAnswerResponse>(GameErrors.InvalidSessionToken);
            }

            if (game.IsTerminatedBySuspension)
            {
                return Result.Failure<SubmitAnswerResponse>(GameErrors.Unavailable);
            }

            GameQuestionSnapshot? question = await _dbContext.GameQuestionSnapshots.AsNoTracking()
                .SingleOrDefaultAsync(snapshot => snapshot.Id == request.QuestionId &&
                                                  snapshot.GameId == game.Id &&
                                                  snapshot.HostAccountId == hostAccountId,
                    cancellationToken);
            if (question is null)
            {
                return Result.Failure<SubmitAnswerResponse>(GameErrors.NotCurrentQuestion);
            }

            // A committed submission survives a lost response, deadline expiry, and phase advance.
            bool alreadyAnswered = await _dbContext.AnswerSubmissions.AsNoTracking().AnyAsync(
                answer => answer.GameId == game.Id && answer.GameQuestionId == question.Id &&
                          answer.ParticipantId == participant.Id, cancellationToken);
            if (alreadyAnswered)
            {
                return Result.Success(new SubmitAnswerResponse(true, true));
            }

            if (isRateLimited)
            {
                return Result.Failure<SubmitAnswerResponse>(GameErrors.TooManyAnswerAttempts);
            }

            if (game.Status != GameStatus.QuestionActive)
            {
                return Result.Failure<SubmitAnswerResponse>(GameErrors.InvalidStateTransition);
            }
            if (question.OrderIndex != game.CurrentQuestionIndex)
            {
                return Result.Failure<SubmitAnswerResponse>(GameErrors.NotCurrentQuestion);
            }

            // PostgreSQL time is the common authority across nodes, sampled after the lock wait.
            DateTimeOffset acceptedAt = await _dbContext.Database
                .SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
                .SingleAsync(cancellationToken);
            if (game.HostGraceExpiresAt <= acceptedAt)
            {
                return Result.Failure<SubmitAnswerResponse>(GameErrors.Unavailable);
            }
            if (question.StartedAt is not DateTimeOffset startedAt ||
                question.EndsAt is not DateTimeOffset endsAt)
            {
                throw new InvalidOperationException("An active question has no timer bounds.");
            }
            if (acceptedAt > endsAt)
            {
                return Result.Failure<SubmitAnswerResponse>(GameErrors.AnswerTooLate);
            }

            List<GameChoiceSnapshot> choices = await _dbContext.GameChoiceSnapshots.AsNoTracking()
                .Where(choice => choice.GameQuestionId == question.Id &&
                                 choice.HostAccountId == hostAccountId)
                .ToListAsync(cancellationToken);
            List<Guid> submittedChoices = request.ChoiceIds.Distinct().ToList();
            HashSet<Guid> validChoiceIds = choices.Select(choice => choice.Id).ToHashSet();
            if (submittedChoices.Any(choiceId => !validChoiceIds.Contains(choiceId)))
            {
                return Result.Failure<SubmitAnswerResponse>(GameErrors.InvalidChoices);
            }

            List<Guid> correctChoiceIds = choices.Where(choice => choice.IsCorrect)
                .Select(choice => choice.Id).ToList();
            bool isCorrect = ScoringEngine.EvaluateCorrectness(submittedChoices, correctChoiceIds);
            TimeSpan elapsed = acceptedAt - startedAt;
            int responseTimeMs = (int)Math.Clamp(
                elapsed.TotalMilliseconds, 0, question.DurationSeconds * 1000.0);
            int points = ScoringEngine.CalculatePoints(
                question.BasePoints, question.DurationSeconds, elapsed, isCorrect);

            Guid submissionId = Guid.NewGuid();
            _dbContext.AnswerSubmissions.Add(new AnswerSubmission
            {
                Id = submissionId,
                HostAccountId = hostAccountId,
                GameId = game.Id,
                GameQuestionId = question.Id,
                ParticipantId = participant.Id,
                SubmittedAt = acceptedAt,
                ResponseTimeMs = responseTimeMs,
                IsCorrect = isCorrect,
                PointsAwarded = points
            });
            foreach (Guid choiceId in submittedChoices)
            {
                _dbContext.AnswerSubmissionChoices.Add(new AnswerSubmissionChoice
                {
                    AnswerSubmissionId = submissionId,
                    HostAccountId = hostAccountId,
                    GameQuestionId = question.Id,
                    GameChoiceId = choiceId
                });
            }

            checked
            {
                participant.TotalScore += points;
            }
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Keep the shared question counter update at the end of the answer transaction.
            // PostgreSQL serializes only this short update across different participants.
            await _dbContext.GameQuestionSnapshots
                .Where(snapshot => snapshot.Id == question.Id &&
                                   snapshot.GameId == game.Id &&
                                   snapshot.HostAccountId == hostAccountId)
                .ExecuteUpdateAsync(setter => setter.SetProperty(
                    snapshot => snapshot.AcceptedAnswerCount,
                    snapshot => snapshot.AcceptedAnswerCount + 1), cancellationToken);
            int acceptedAnswerCount = await _dbContext.GameQuestionSnapshots.AsNoTracking()
                .Where(snapshot => snapshot.Id == question.Id &&
                                   snapshot.GameId == game.Id &&
                                   snapshot.HostAccountId == hostAccountId)
                .Select(snapshot => snapshot.AcceptedAnswerCount)
                .SingleAsync(cancellationToken);

            if (question.EffectiveEligibleParticipantCount > 0 &&
                acceptedAnswerCount == question.EffectiveEligibleParticipantCount)
            {
                (GameQuestionSnapshot materializedQuestion, List<GameChoiceSnapshot> materializedChoices) =
                    await QuestionResultsMaterializer.MaterializeAsync(
                        _dbContext, game, acceptedAt, cancellationToken);
                game.Status = GameStatus.QuestionResults;
                game.StateVersion++;
                stateVersion = game.StateVersion;
                List<QuestionChoiceResultDto> choiceResults = materializedChoices
                    .Select(choice => new QuestionChoiceResultDto(
                        choice.Id, choice.OrderIndex, choice.Text, choice.IsCorrect, choice.SelectionCount))
                    .ToList();
                endedEvent = new QuestionEndedEvent(
                    game.Id, stateVersion, question.Id, question.OrderIndex,
                    materializedQuestion.AcceptedAnswerCount,
                    materializedQuestion.EffectiveEligibleParticipantCount, acceptedAt,
                    choiceResults.Where(choice => choice.IsCorrect)
                        .Select(choice => choice.ChoiceId).ToList(),
                    choiceResults);

                // Scorecard Materialization (SCORE-RES-002) - Prepares individual scorecard projection for each non-removed participant
                personalResults = await QuestionResultsMaterializer.MaterializePersonalResultsAsync(
                    _dbContext, game, materializedQuestion, stateVersion, cancellationToken);

                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        if (endedEvent is not null)
        {
            // Post-Commit Broadcast Pattern - Fans out QuestionEnded event and personal scorecards to players only after database transaction is durable
            await _notificationService.PublishQuestionEndedWithPersonalResultsAsync(
                hostAccountId, request.GameId, stateVersion, endedEvent, personalResults ?? [], CancellationToken.None);
        }
        return Result.Success(new SubmitAnswerResponse(true, false));
    }
}
