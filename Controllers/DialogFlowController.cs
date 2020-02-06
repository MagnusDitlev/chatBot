using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Google.Cloud.Dialogflow.V2;
using Google.Protobuf;
using System.IO;

namespace FairFit_DialogFlow_Fullfilmment.Controllers
{
    [ApiController]
    public class DialogFlowController : Controller
    {
        private static readonly JsonParser jsonParser = new JsonParser(JsonParser.Settings.Default.WithIgnoreUnknownFields(true));
        private readonly ILogger<DialogFlowController> _logger;
        private readonly NoAccessHandler _noAccess;

        public DialogFlowController(ILogger<DialogFlowController> logger, NoAccessHandler noAccess)
        {
            _logger = logger;
            _noAccess = noAccess;
        }

        [HttpPost]
        [Route("api/[controller]")]
        public async Task<ContentResult> DialogAction()
        {
            WebhookRequest request;
            using (var reader = new StreamReader(Request.Body))
            {
                request = jsonParser.Parse<WebhookRequest>(await reader.ReadToEndAsync());
            }

            var response = await HandleRequest(request);
            var responseJson = response.ToString();
            return Content(responseJson, "application/json");
        }

        private async Task<WebhookResponse> HandleRequest (WebhookRequest request)
        {
            switch (request.QueryResult.Intent.DisplayName)
            {
                case "NoAccess-Phone":
                    return await _noAccess.HandleEnterPhone(request);
                case "NoAccess-Phone-SmsCodeSent-EnterCode":
                    return await _noAccess.HandleEnterPhoneCode(request);
                case "NoAccess-Pin":
                    return await _noAccess.HandleCreatePin(request);
                default:
                    return HandleUnknownIntent(request);
            }
        }

        private WebhookResponse HandleUnknownIntent (WebhookRequest request)
        {
            throw new Exception($"Unknown intent {request.QueryResult.Intent.DisplayName}");
        }
    }
}
