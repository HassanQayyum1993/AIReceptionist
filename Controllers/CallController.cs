using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using AIReceptionist.Api.Services;

namespace AIReceptionist.Api.Controllers;
//7f913bd9a5f3dca888d5592881975d4e
[ApiController]
[Route("api/call")]
public class CallController : ControllerBase
{
    private readonly AppSettings _settings;

    private readonly TwilioValidator _validator;
    private readonly ILogger<CallController> _log;

    public CallController(IOptions<AppSettings> opts, TwilioValidator validator, ILogger<CallController> log)
    {
        _settings = opts.Value;
        _validator = validator;
        _log = log;
    }

    [HttpPost("incoming")]
    public async Task<IActionResult> Incoming()
    {
        try
        {
            _log.LogInformation("Incoming Twilio webhook: Path={Path}, HasForm={HasForm}", Request.Path, Request.HasFormContentType);
            if (Request.HasFormContentType)
            {
                var from = Request.Form["From"].ToString();
                var callSid = Request.Form["CallSid"].ToString();
                _log.LogDebug("Twilio form fields: From={From}, CallSid={CallSid}", from, callSid);
            }

            var valid = _validator.Validate(Request);
            _log.LogInformation("Twilio signature validation result: {Valid}", valid);
            if (!valid) return Unauthorized();

            // Read the streaming URL from configuration
            var streamUrl = _settings.Streaming?.Url;
            if (string.IsNullOrEmpty(streamUrl))
            {
                _log.LogWarning("Streaming URL not configured; returning placeholder TwiML. Configure Streaming:Url in appsettings or env vars.");
                streamUrl = "wss://your-server.example.com/api/call/stream";
            }

            if (!Uri.TryCreate(streamUrl, UriKind.Absolute, out var _))
            {
                _log.LogError("Configured Streaming:Url is not a valid absolute URI: {Url}", streamUrl);
                return Problem(title: "Invalid streaming URL", detail: "The configured streaming URL is invalid. Please update Streaming:Url configuration.", statusCode: 500);
            }

            var twiml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                        $"<Response><Start><Stream url=\"{System.Net.WebUtility.HtmlEncode(streamUrl)}\"/></Start></Response>";
            _log.LogDebug("Returning TwiML with stream url {Url}", streamUrl);
            return Content(twiml, "application/xml");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error handling incoming Twilio webhook");
            return Problem(title: "Webhook processing error", detail: "An error occurred while processing the incoming webhook. Please try again.", statusCode: 500);
        }
    }
}
