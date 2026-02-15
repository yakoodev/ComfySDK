using ComfySdk.Diagnostics;

var request = new HttpRequestMessage(
    HttpMethod.Get,
    "https://api.comfy.org/api/history?token=super-secret-token&prompt_id=42");
request.Headers.TryAddWithoutValidation("Authorization", "Bearer super-secret-token");
request.Headers.TryAddWithoutValidation("Cookie", "session=secret-session");

var masked = SecretMasker.FormatRequestForLog(request);

AssertDoesNotContain(masked, "super-secret-token");
AssertDoesNotContain(masked, "secret-session");

Console.WriteLine("MaskingTests: PASS");
return 0;

static void AssertDoesNotContain(string text, string forbidden)
{
    if (text.Contains(forbidden, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected masked text to not contain '{forbidden}'. Actual: {text}");
    }
}
