namespace Skight.AgentPlatform.FSharp

open System
open System.Net.Http
open System.Text
open System.Text.Json
open System.Threading.Tasks

module IntegrationTools =

    let private httpClient = new HttpClient()

    let sendWebhookAsync (argsJson: string) : Task<string> =
        async {
            try
                let doc = JsonDocument.Parse argsJson
                use _ = doc
                let root = doc.RootElement
                match root.TryGetProperty("url") with
                | true, urlProp ->
                    let url = urlProp.GetString()
                    let methodStr = if root.TryGetProperty("method") <> (false, Unchecked.defaultof<_>) then root.GetProperty("method").GetString().ToUpper() else "POST"
                    let payload = if root.TryGetProperty("payload") <> (false, Unchecked.defaultof<_>) then root.GetProperty("payload").ToString() else "{}"

                    use request = new HttpRequestMessage(HttpMethod(methodStr), url)
                    request.Content <- new StringContent(payload, Encoding.UTF8, "application/json")

                    if root.TryGetProperty("headers") <> (false, Unchecked.defaultof<_>) && root.GetProperty("headers").ValueKind = JsonValueKind.Object then
                        for prop in root.GetProperty("headers").EnumerateObject() do
                            request.Headers.TryAddWithoutValidation(prop.Name, prop.Value.ToString()) |> ignore

                    let! response = httpClient.SendAsync(request) |> Async.AwaitTask
                    let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                    let truncated = ToolSecurity.truncateOutput 5000 body
                    return sprintf "Webhook HTTP %d (%s):\n%s" (int response.StatusCode) response.ReasonPhrase truncated
                | false, _ -> return "Error: Missing 'url' argument."
            with ex -> return sprintf "Error sending webhook: %s" ex.Message
        } |> Async.StartAsTask
