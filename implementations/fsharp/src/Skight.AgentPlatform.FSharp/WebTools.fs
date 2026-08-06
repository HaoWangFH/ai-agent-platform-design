namespace Skight.AgentPlatform.FSharp

open System
open System.Net.Http
open System.Text.Json
open System.Text.RegularExpressions
open System.Threading.Tasks

module WebTools =

    let private httpClient = new HttpClient()

    let private stripHtml (html: string) =
        if String.IsNullOrWhiteSpace(html) then ""
        else
            let clean1 = Regex.Replace(html, @"<(script|style)[^>]*?>.*?</\1>", "", RegexOptions.IgnoreCase ||| RegexOptions.Singleline)
            let clean2 = Regex.Replace(clean1, @"<[^>]+>", " ")
            Regex.Replace(clean2, @"\s+", " ").Trim()

    let fetchUrlContentAsync (argsJson: string) : Task<string> =
        async {
            try
                let doc = JsonDocument.Parse argsJson
                use _ = doc
                let root = doc.RootElement
                match root.TryGetProperty("url") with
                | true, urlProp ->
                    let url = urlProp.GetString()
                    let! response = httpClient.GetAsync(url) |> Async.AwaitTask
                    if not response.IsSuccessStatusCode then
                        return sprintf "Error fetching URL (%O): %s" response.StatusCode response.ReasonPhrase
                    else
                        let! html = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                        let text = stripHtml html
                        return ToolSecurity.truncateOutput 50_000 text
                | false, _ ->
                    return "Error: Missing 'url' argument."
            with ex ->
                return sprintf "Error executing web_fetch_content: %s" ex.Message
        } |> Async.StartAsTask
