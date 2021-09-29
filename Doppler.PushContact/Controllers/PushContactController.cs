using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using Doppler.PushContact.Models;
using Doppler.PushContact.DopplerSecurity;
using System;
using Doppler.PushContact.Services;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Doppler.PushContact.Services.Messages;

namespace Doppler.PushContact.Controllers
{
    [Authorize(Policies.ONLY_SUPERUSER)]
    [ApiController]
    public class PushContactController : ControllerBase
    {
        private readonly IPushContactService _pushContactService;
        private readonly IMessageSender _messageSender;

        public PushContactController(IPushContactService pushContactService, IMessageSender messageSender)
        {
            _pushContactService = pushContactService;
            _messageSender = messageSender;
        }

        [AllowAnonymous]
        [HttpPost]
        [Route("push-contacts")]
        public async Task<IActionResult> Add([FromBody] PushContactModel pushContactModel)
        {
            await _pushContactService.AddAsync(pushContactModel);

            return Ok();
        }

        [HttpGet]
        [Route("push-contacts")]
        public async Task<IActionResult> GetBy([FromQuery, Required] string domain, [FromQuery] string email, [FromQuery] DateTime? modifiedFrom, [FromQuery] DateTime? modifiedTo)
        {
            var pushContactFilter = new PushContactFilter(domain, email, modifiedFrom, modifiedTo);

            var pushContacts = await _pushContactService.GetAsync(pushContactFilter);

            if (pushContacts == null || !pushContacts.Any())
            {
                return NotFound();
            }

            return Ok(pushContacts);
        }

        [HttpDelete]
        [Route("push-contacts/_bulk")]
        public async Task<IActionResult> BulkDelete([FromBody] IEnumerable<string> deviceTokens)
        {
            var deletedCount = await _pushContactService.DeleteByDeviceTokenAsync(deviceTokens);

            return Ok(deletedCount);
        }

        [AllowAnonymous]
        [HttpPut]
        [Route("push-contacts/{deviceToken}/email")]
        public async Task<IActionResult> UpdateEmail([FromRoute] string deviceToken, [FromBody] string email)
        {
            await _pushContactService.UpdateEmailAsync(deviceToken, email);

            return Ok();
        }

        [HttpPost]
        [Route("push-contacts/{domain}/message")]
        public async Task<IActionResult> Message([FromRoute] string domain, [FromBody] Message message)
        {
            var deviceTokens = await _pushContactService.GetAllDeviceTokensByDomainAsync(domain);

            var sendMessageResult = await _messageSender.SendAsync(message.Title, message.Body, deviceTokens);

            var notValidTargetDeviceToken = sendMessageResult
                .SendMessageTargetResult?
                .Where(x => !x.IsValidTargetDeviceToken)
                .Select(x => x.TargetDeviceToken);

            if (notValidTargetDeviceToken != null && notValidTargetDeviceToken.Any())
            {
                await _pushContactService.DeleteByDeviceTokenAsync(notValidTargetDeviceToken);
            }

            // TODO: add history events related to sent messages,
            // response a MessageId and run all steps asynchronous

            throw new NotImplementedException();
        }
    }
}
