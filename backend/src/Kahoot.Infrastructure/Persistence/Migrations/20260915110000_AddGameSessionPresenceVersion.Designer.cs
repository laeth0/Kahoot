using System;
using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kahoot.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(KahootDbContext))]
    [Migration("20260915110000_AddGameSessionPresenceVersion")]
    partial class AddGameSessionPresenceVersion
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.12")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Kahoot.Domain.Games.Answer", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("GameSessionId")
                        .HasColumnType("uuid")
                        .HasColumnName("game_session_id");

                    b.Property<bool>("IsCorrect")
                        .HasColumnType("boolean")
                        .HasColumnName("is_correct");

                    b.Property<Guid>("ParticipantId")
                        .HasColumnType("uuid")
                        .HasColumnName("participant_id");

                    b.Property<int>("PointsAwarded")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(0)
                        .HasColumnName("points_awarded");

                    b.Property<Guid>("QuestionId")
                        .HasColumnType("uuid")
                        .HasColumnName("question_id");

                    b.Property<int>("ResponseTimeMs")
                        .HasColumnType("integer")
                        .HasColumnName("response_time_ms");

                    b.Property<DateTime>("SubmittedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("submitted_at");

                    b.HasKey("Id")
                        .HasName("pk_answers");

                    b.HasIndex("ParticipantId")
                        .HasDatabaseName("ix_answer_participant_id");

                    b.HasIndex("QuestionId")
                        .HasDatabaseName("ix_answers_question_id");

                    b.HasIndex("GameSessionId", "QuestionId")
                        .HasDatabaseName("ix_answer_game_question");

                    b.HasIndex("GameSessionId", "QuestionId", "ParticipantId")
                        .IsUnique()
                        .HasDatabaseName("uq_answer_participant_question");

                    b.ToTable("answers", null, t =>
                        {
                            t.HasCheckConstraint("ck_answer_points_awarded", "points_awarded >= 0");

                            t.HasCheckConstraint("ck_answer_response_time_ms", "response_time_ms >= 0");
                        });
                });

            modelBuilder.Entity("Kahoot.Domain.Games.AnswerSelectedChoice", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("AnswerId")
                        .HasColumnType("uuid")
                        .HasColumnName("answer_id");

                    b.Property<Guid>("SelectedChoiceId")
                        .HasColumnType("uuid")
                        .HasColumnName("selected_choice_id");

                    b.HasKey("Id")
                        .HasName("pk_answer_selected_choices");

                    b.HasIndex("AnswerId")
                        .HasDatabaseName("ix_answer_selected_choice_answer_id");

                    b.HasIndex("SelectedChoiceId")
                        .HasDatabaseName("ix_answer_selected_choice_choice_id");

                    b.HasIndex("AnswerId", "SelectedChoiceId")
                        .IsUnique()
                        .HasDatabaseName("uq_answer_selected_choice");

                    b.ToTable("answer_selected_choices", (string)null);
                });

            modelBuilder.Entity("Kahoot.Domain.Games.GameChoiceSnapshot", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<bool>("IsCorrect")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(false)
                        .HasColumnName("is_correct");

                    b.Property<int>("OrderIndex")
                        .HasColumnType("integer")
                        .HasColumnName("order_index");

                    b.Property<Guid>("QuestionSnapshotId")
                        .HasColumnType("uuid")
                        .HasColumnName("question_snapshot_id");

                    b.Property<Guid?>("SourceChoiceId")
                        .HasColumnType("uuid")
                        .HasColumnName("source_choice_id");

                    b.Property<string>("Text")
                        .IsRequired()
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)")
                        .HasColumnName("text");

                    b.HasKey("Id")
                        .HasName("pk_game_choice_snapshots");

                    b.HasIndex("QuestionSnapshotId")
                        .HasDatabaseName("ix_game_choice_snapshot_question_id");

                    b.HasIndex("QuestionSnapshotId", "OrderIndex")
                        .IsUnique()
                        .HasDatabaseName("uq_game_choice_snapshot_order");

                    b.ToTable("game_choice_snapshots", (string)null);
                });

            modelBuilder.Entity("Kahoot.Domain.Games.GameQuestionSnapshot", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("GameSessionId")
                        .HasColumnType("uuid")
                        .HasColumnName("game_session_id");

                    b.Property<string>("ImageUrl")
                        .HasMaxLength(2048)
                        .HasColumnType("character varying(2048)")
                        .HasColumnName("image_url");

                    b.Property<int>("OrderIndex")
                        .HasColumnType("integer")
                        .HasColumnName("order_index");

                    b.Property<int>("Points")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(1000)
                        .HasColumnName("points");

                    b.Property<Guid?>("SourceQuestionId")
                        .HasColumnType("uuid")
                        .HasColumnName("source_question_id");

                    b.Property<string>("Text")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)")
                        .HasColumnName("text");

                    b.Property<int>("TimeLimitSeconds")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(20)
                        .HasColumnName("time_limit_seconds");

                    b.HasKey("Id")
                        .HasName("pk_game_question_snapshots");

                    b.HasIndex("GameSessionId")
                        .HasDatabaseName("ix_game_question_snapshot_session_id");

                    b.HasIndex("GameSessionId", "OrderIndex")
                        .IsUnique()
                        .HasDatabaseName("uq_game_question_snapshot_order");

                    b.ToTable("game_question_snapshots", null, t =>
                        {
                            t.HasCheckConstraint("ck_game_question_snapshot_points", "points >= 0");

                            t.HasCheckConstraint("ck_game_question_snapshot_time_limit_seconds", "time_limit_seconds >= 5 AND time_limit_seconds <= 300");
                        });
                });

            modelBuilder.Entity("Kahoot.Domain.Games.GameSession", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<int>("CurrentQuestionAnsweredCount")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(0)
                        .HasColumnName("current_question_answered_count");

                    b.Property<int>("CurrentQuestionEligibleCount")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(0)
                        .HasColumnName("current_question_eligible_count");

                    b.Property<DateTime?>("CurrentQuestionEndsAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("current_question_ends_at");

                    b.Property<Guid?>("CurrentQuestionId")
                        .HasColumnType("uuid")
                        .HasColumnName("current_question_id");

                    b.Property<int?>("CurrentQuestionIndex")
                        .HasColumnType("integer")
                        .HasColumnName("current_question_index");

                    b.Property<DateTime?>("CurrentQuestionStartedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("current_question_started_at");

                    b.Property<DateTime?>("FinishedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("finished_at");

                    b.Property<Guid>("HostId")
                        .HasColumnType("uuid")
                        .HasColumnName("host_id");

                    b.Property<string>("Pin")
                        .IsRequired()
                        .HasMaxLength(8)
                        .HasColumnType("character varying(8)")
                        .HasColumnName("pin");

                    b.Property<long>("PresenceVersion")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .HasDefaultValue(0L)
                        .HasColumnName("presence_version");

                    b.Property<Guid>("QuizId")
                        .HasColumnType("uuid")
                        .HasColumnName("quiz_id");

                    b.Property<string>("QuizTitle")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("quiz_title");

                    b.Property<DateTime?>("StartedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("started_at");

                    b.Property<string>("Status")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)")
                        .HasColumnName("status")
                        .HasDefaultValueSql("'Created'");

                    b.Property<uint>("Version")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("xid")
                        .HasColumnName("xmin");

                    b.HasKey("Id")
                        .HasName("pk_game_sessions");

                    b.HasIndex("CurrentQuestionId")
                        .HasDatabaseName("ix_game_session_current_question_id");

                    b.HasIndex("HostId")
                        .HasDatabaseName("ix_game_session_host_id");

                    b.HasIndex("Pin")
                        .IsUnique()
                        .HasDatabaseName("uq_game_session_active_pin")
                        .HasFilter("status <> 'Finished'");

                    b.HasIndex("QuizId")
                        .HasDatabaseName("ix_game_session_quiz_id");

                    b.HasIndex("Status")
                        .HasDatabaseName("ix_game_session_status");

                    b.ToTable("game_sessions", null, t =>
                        {
                            t.HasCheckConstraint("ck_game_session_question_window", "current_question_ends_at IS NULL OR current_question_started_at IS NULL OR current_question_ends_at >= current_question_started_at");
                        });
                });

            modelBuilder.Entity("Kahoot.Domain.Games.Participant", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<string>("ConnectionId")
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)")
                        .HasColumnName("connection_id");

                    b.Property<Guid>("GameSessionId")
                        .HasColumnType("uuid")
                        .HasColumnName("game_session_id");

                    b.Property<bool>("IsRemoved")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(false)
                        .HasColumnName("is_removed");

                    b.Property<int?>("LastRank")
                        .HasColumnType("integer")
                        .HasColumnName("last_rank");

                    b.Property<DateTime>("LastSeenAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("last_seen_at");

                    b.Property<string>("Nickname")
                        .IsRequired()
                        .HasMaxLength(30)
                        .HasColumnType("character varying(30)")
                        .HasColumnName("nickname");

                    b.Property<string>("NicknameNormalized")
                        .IsRequired()
                        .HasMaxLength(30)
                        .HasColumnType("character varying(30)")
                        .HasColumnName("nickname_normalized");

                    b.Property<DateTime?>("RemovedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("removed_at");

                    b.Property<string>("SessionTokenHash")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)")
                        .HasColumnName("session_token_hash");

                    b.Property<int>("TotalScore")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(0)
                        .HasColumnName("total_score");

                    b.HasKey("Id")
                        .HasName("pk_participants");

                    b.HasIndex("ConnectionId")
                        .HasDatabaseName("ix_participant_connection_id");

                    b.HasIndex("GameSessionId")
                        .HasDatabaseName("ix_participant_game_session_id");

                    b.HasIndex("SessionTokenHash")
                        .IsUnique()
                        .HasDatabaseName("uq_participant_session_token");

                    b.HasIndex("GameSessionId", "NicknameNormalized")
                        .IsUnique()
                        .HasDatabaseName("uq_participant_game_nickname")
                        .HasFilter("is_removed = false");

                    b.HasIndex("GameSessionId", "TotalScore")
                        .HasDatabaseName("ix_participant_game_score");

                    b.ToTable("participants", null, t =>
                        {
                            t.HasCheckConstraint("ck_participant_total_score", "total_score >= 0");
                        });
                });

            modelBuilder.Entity("Kahoot.Domain.Hosts.Host", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<string>("PasswordHash")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("character varying(256)")
                        .HasColumnName("password_hash");

                    b.Property<string>("Username")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)")
                        .HasColumnName("username");

                    b.HasKey("Id")
                        .HasName("pk_hosts");

                    b.HasIndex("Username")
                        .IsUnique()
                        .HasDatabaseName("uq_host_username");

                    b.ToTable("hosts", (string)null);
                });

            modelBuilder.Entity("Kahoot.Domain.Hosts.RefreshToken", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<DateTime>("ExpiresAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("expires_at");

                    b.Property<Guid>("FamilyId")
                        .HasColumnType("uuid")
                        .HasColumnName("family_id");

                    b.Property<Guid>("HostId")
                        .HasColumnType("uuid")
                        .HasColumnName("host_id");

                    b.Property<Guid?>("ReplacedByTokenId")
                        .HasColumnType("uuid")
                        .HasColumnName("replaced_by_token_id");

                    b.Property<DateTime?>("RevokedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("revoked_at");

                    b.Property<string>("TokenHash")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)")
                        .HasColumnName("token_hash");

                    b.HasKey("Id")
                        .HasName("pk_refresh_tokens");

                    b.HasIndex("ExpiresAt")
                        .HasDatabaseName("ix_refresh_tokens_expires_at");

                    b.HasIndex("FamilyId")
                        .HasDatabaseName("ix_refresh_tokens_family_id");

                    b.HasIndex("HostId")
                        .HasDatabaseName("ix_refresh_token_host_id");

                    b.HasIndex("ReplacedByTokenId")
                        .HasDatabaseName("ix_refresh_tokens_replaced_by_token_id");

                    b.HasIndex("TokenHash")
                        .IsUnique()
                        .HasDatabaseName("uq_refresh_token_hash");

                    b.HasIndex("FamilyId", "RevokedAt")
                        .HasDatabaseName("ix_refresh_tokens_family_revoked");

                    b.ToTable("refresh_tokens", (string)null);
                });

            modelBuilder.Entity("Kahoot.Domain.Quizzes.Choice", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<bool>("IsCorrect")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(false)
                        .HasColumnName("is_correct");

                    b.Property<int>("OrderIndex")
                        .HasColumnType("integer")
                        .HasColumnName("order_index");

                    b.Property<Guid>("QuestionId")
                        .HasColumnType("uuid")
                        .HasColumnName("question_id");

                    b.Property<string>("Text")
                        .IsRequired()
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)")
                        .HasColumnName("text");

                    b.HasKey("Id")
                        .HasName("pk_choices");

                    b.HasIndex("QuestionId")
                        .HasDatabaseName("ix_choice_question_id");

                    b.HasIndex("QuestionId", "OrderIndex")
                        .IsUnique()
                        .HasDatabaseName("uq_choice_question_order");

                    b.ToTable("choices", (string)null);
                });

            modelBuilder.Entity("Kahoot.Domain.Quizzes.Question", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<string>("ImageUrl")
                        .HasMaxLength(2048)
                        .HasColumnType("character varying(2048)")
                        .HasColumnName("image_url");

                    b.Property<int>("OrderIndex")
                        .HasColumnType("integer")
                        .HasColumnName("order_index");

                    b.Property<int>("Points")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(1000)
                        .HasColumnName("points");

                    b.Property<Guid>("QuizId")
                        .HasColumnType("uuid")
                        .HasColumnName("quiz_id");

                    b.Property<string>("Text")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)")
                        .HasColumnName("text");

                    b.Property<int>("TimeLimitSeconds")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(20)
                        .HasColumnName("time_limit_seconds");

                    b.HasKey("Id")
                        .HasName("pk_questions");

                    b.HasIndex("QuizId")
                        .HasDatabaseName("ix_question_quiz_id");

                    b.HasIndex("QuizId", "OrderIndex")
                        .IsUnique()
                        .HasDatabaseName("uq_question_quiz_order");

                    b.ToTable("questions", null, t =>
                        {
                            t.HasCheckConstraint("ck_question_points", "points >= 0");

                            t.HasCheckConstraint("ck_question_time_limit_seconds", "time_limit_seconds >= 5 AND time_limit_seconds <= 300");
                        });
                });

            modelBuilder.Entity("Kahoot.Domain.Quizzes.Quiz", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<string>("Description")
                        .HasMaxLength(1000)
                        .HasColumnType("character varying(1000)")
                        .HasColumnName("description");

                    b.Property<Guid>("HostId")
                        .HasColumnType("uuid")
                        .HasColumnName("host_id");

                    b.Property<bool>("IsPublished")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(false)
                        .HasColumnName("is_published");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("title");

                    b.HasKey("Id")
                        .HasName("pk_quizzes");

                    b.HasIndex("HostId")
                        .HasDatabaseName("ix_quiz_host_id");

                    b.ToTable("quizzes", (string)null);
                });

            modelBuilder.Entity("Kahoot.Domain.Games.Answer", b =>
                {
                    b.HasOne("Kahoot.Domain.Games.GameSession", "GameSession")
                        .WithMany("Answers")
                        .HasForeignKey("GameSessionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_answers_game_sessions_game_session_id");

                    b.HasOne("Kahoot.Domain.Games.Participant", "Participant")
                        .WithMany("Answers")
                        .HasForeignKey("ParticipantId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired()
                        .HasConstraintName("fk_answers_participants_participant_id");

                    b.HasOne("Kahoot.Domain.Games.GameQuestionSnapshot", "Question")
                        .WithMany("Answers")
                        .HasForeignKey("QuestionId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired()
                        .HasConstraintName("fk_answers_game_question_snapshots_question_id");

                    b.Navigation("GameSession");

                    b.Navigation("Participant");

                    b.Navigation("Question");
                });

            modelBuilder.Entity("Kahoot.Domain.Games.AnswerSelectedChoice", b =>
                {
                    b.HasOne("Kahoot.Domain.Games.Answer", "Answer")
                        .WithMany("SelectedChoices")
                        .HasForeignKey("AnswerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_answer_selected_choices_answers_answer_id");

                    b.HasOne("Kahoot.Domain.Games.GameChoiceSnapshot", "SelectedChoice")
                        .WithMany("SelectedChoices")
                        .HasForeignKey("SelectedChoiceId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired()
                        .HasConstraintName("fk_answer_selected_choices_game_choice_snapshots_selected_choi");

                    b.Navigation("Answer");

                    b.Navigation("SelectedChoice");
                });

            modelBuilder.Entity("Kahoot.Domain.Games.GameChoiceSnapshot", b =>
                {
                    b.HasOne("Kahoot.Domain.Games.GameQuestionSnapshot", "QuestionSnapshot")
                        .WithMany("Choices")
                        .HasForeignKey("QuestionSnapshotId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_game_choice_snapshots_game_question_snapshots_question_snap");

                    b.Navigation("QuestionSnapshot");
                });

            modelBuilder.Entity("Kahoot.Domain.Games.GameQuestionSnapshot", b =>
                {
                    b.HasOne("Kahoot.Domain.Games.GameSession", "GameSession")
                        .WithMany("QuestionSnapshots")
                        .HasForeignKey("GameSessionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_game_question_snapshots_game_sessions_game_session_id");

                    b.Navigation("GameSession");
                });

            modelBuilder.Entity("Kahoot.Domain.Games.GameSession", b =>
                {
                    b.HasOne("Kahoot.Domain.Games.GameQuestionSnapshot", "CurrentQuestion")
                        .WithMany()
                        .HasForeignKey("CurrentQuestionId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .HasConstraintName("fk_game_sessions_game_question_snapshots_current_question_id");

                    b.HasOne("Kahoot.Domain.Hosts.Host", "Host")
                        .WithMany()
                        .HasForeignKey("HostId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired()
                        .HasConstraintName("fk_game_sessions_hosts_host_id");

                    b.HasOne("Kahoot.Domain.Quizzes.Quiz", "Quiz")
                        .WithMany()
                        .HasForeignKey("QuizId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired()
                        .HasConstraintName("fk_game_sessions_quizzes_quiz_id");

                    b.Navigation("CurrentQuestion");

                    b.Navigation("Host");

                    b.Navigation("Quiz");
                });

            modelBuilder.Entity("Kahoot.Domain.Games.Participant", b =>
                {
                    b.HasOne("Kahoot.Domain.Games.GameSession", "GameSession")
                        .WithMany("Participants")
                        .HasForeignKey("GameSessionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_participants_game_sessions_game_session_id");

                    b.Navigation("GameSession");
                });

            modelBuilder.Entity("Kahoot.Domain.Hosts.RefreshToken", b =>
                {
                    b.HasOne("Kahoot.Domain.Hosts.Host", "Host")
                        .WithMany("RefreshTokens")
                        .HasForeignKey("HostId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_refresh_tokens_hosts_host_id");

                    b.HasOne("Kahoot.Domain.Hosts.RefreshToken", null)
                        .WithMany()
                        .HasForeignKey("ReplacedByTokenId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .HasConstraintName("fk_refresh_tokens_refresh_tokens_replaced_by_token_id");

                    b.Navigation("Host");
                });

            modelBuilder.Entity("Kahoot.Domain.Quizzes.Choice", b =>
                {
                    b.HasOne("Kahoot.Domain.Quizzes.Question", "Question")
                        .WithMany("Choices")
                        .HasForeignKey("QuestionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_choices_questions_question_id");

                    b.Navigation("Question");
                });

            modelBuilder.Entity("Kahoot.Domain.Quizzes.Question", b =>
                {
                    b.HasOne("Kahoot.Domain.Quizzes.Quiz", "Quiz")
                        .WithMany("Questions")
                        .HasForeignKey("QuizId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_questions_quizzes_quiz_id");

                    b.Navigation("Quiz");
                });

            modelBuilder.Entity("Kahoot.Domain.Quizzes.Quiz", b =>
                {
                    b.HasOne("Kahoot.Domain.Hosts.Host", "Host")
                        .WithMany("Quizzes")
                        .HasForeignKey("HostId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_quizzes_hosts_host_id");

                    b.Navigation("Host");
                });

            modelBuilder.Entity("Kahoot.Domain.Games.Answer", b =>
                {
                    b.Navigation("SelectedChoices");
                });

            modelBuilder.Entity("Kahoot.Domain.Games.GameChoiceSnapshot", b =>
                {
                    b.Navigation("SelectedChoices");
                });

            modelBuilder.Entity("Kahoot.Domain.Games.GameQuestionSnapshot", b =>
                {
                    b.Navigation("Answers");

                    b.Navigation("Choices");
                });

            modelBuilder.Entity("Kahoot.Domain.Games.GameSession", b =>
                {
                    b.Navigation("Answers");

                    b.Navigation("Participants");

                    b.Navigation("QuestionSnapshots");
                });

            modelBuilder.Entity("Kahoot.Domain.Games.Participant", b =>
                {
                    b.Navigation("Answers");
                });

            modelBuilder.Entity("Kahoot.Domain.Hosts.Host", b =>
                {
                    b.Navigation("Quizzes");

                    b.Navigation("RefreshTokens");
                });

            modelBuilder.Entity("Kahoot.Domain.Quizzes.Question", b =>
                {
                    b.Navigation("Choices");
                });

            modelBuilder.Entity("Kahoot.Domain.Quizzes.Quiz", b =>
                {
                    b.Navigation("Questions");
                });
#pragma warning restore 612, 618
        }
    }
}
