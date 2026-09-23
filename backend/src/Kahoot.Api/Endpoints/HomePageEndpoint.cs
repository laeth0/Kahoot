namespace Kahoot.Api.Endpoints;

internal static class HomePageEndpoint
{
    public static WebApplication MapHomePage(this WebApplication app)
    {
        string html = BuildPage(app.Environment.IsDevelopment());

        app.MapGet("/", () => Results.Content(html, "text/html; charset=utf-8"))
            .AllowAnonymous()
            .ExcludeFromDescription();

        return app;
    }

    private static string BuildPage(bool isDevelopment)
    {
        string apiReferenceAction = isDevelopment
            ? """<a class="btn" href="/scalar/v1">Open API reference</a>"""
            : """<p class="note">The interactive API reference is available when the API runs in the Development environment.</p>""";

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="UTF-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                <title>Kahoot-like Platform API</title>
                <style>
                    :root {
                        --card: #ffffff;
                        --text: #09131f;
                        --muted: #475569;
                        --primary: #00629b;
                        --primary-hover: #0284c7;
                        --border: #e2e8f0;
                    }
                    * { box-sizing: border-box; }
                    body {
                        margin: 0;
                        font-family: "Segoe UI", Tahoma, Geneva, Verdana, sans-serif;
                        background: linear-gradient(135deg, #f4f8fc 0%, #e2e8f0 100%);
                        color: var(--text);
                        min-height: 100vh;
                        display: grid;
                        place-items: center;
                        padding: 24px;
                    }
                    .card {
                        width: min(680px, 100%);
                        background: var(--card);
                        border: 1px solid var(--border);
                        border-radius: 16px;
                        padding: 40px 32px;
                        box-shadow: 0 20px 45px rgba(2, 8, 23, 0.12);
                        text-align: center;
                    }
                    .status {
                        display: inline-flex;
                        align-items: center;
                        gap: 8px;
                        font-size: 13px;
                        font-weight: 600;
                        color: #0f766e;
                        background: #d1fae5;
                        border-radius: 999px;
                        padding: 6px 14px;
                        margin-bottom: 20px;
                    }
                    .status::before {
                        content: "";
                        width: 8px;
                        height: 8px;
                        border-radius: 50%;
                        background: #10b981;
                    }
                    h1 {
                        margin: 0 0 12px;
                        font-size: clamp(26px, 4vw, 38px);
                    }
                    p {
                        margin: 0 0 24px;
                        color: var(--muted);
                        font-size: 17px;
                        line-height: 1.6;
                    }
                    .btn {
                        display: inline-block;
                        text-decoration: none;
                        border-radius: 10px;
                        padding: 12px 22px;
                        font-weight: 700;
                        background: var(--primary);
                        color: #ffffff;
                        transition: background .2s ease-in-out;
                    }
                    .btn:hover { background: var(--primary-hover); }
                    .note { font-size: 15px; }
                </style>
            </head>
            <body>
                <main class="card">
                    <span class="status">Running</span>
                    <h1>Kahoot-like Platform API</h1>
                    <p>The service is up and responding.</p>
                    {{apiReferenceAction}}
                </main>
            </body>
            </html>
            """;
    }
}
