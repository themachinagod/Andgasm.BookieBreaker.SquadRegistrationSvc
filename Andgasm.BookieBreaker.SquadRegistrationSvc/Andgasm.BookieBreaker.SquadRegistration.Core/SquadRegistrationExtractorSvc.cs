﻿using Andgasm.BookieBreaker.Harvest.WhoScored;
using Andgasm.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Dynamic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Andgasm.BookieBreaker.SquadRegistration.Core
{
    public class SquadRegistrationExtractorSvc : IHostedService
    {
        static ILogger<SquadRegistrationExtractorSvc> _logger;
        static IBusClient _newClubSeasonAssociationBus;
        static SquadRegistrationHarvester _harvester;

        public SquadRegistrationExtractorSvc(ILogger<SquadRegistrationExtractorSvc> logger, IBusClient newClubSeasonBus, SquadRegistrationHarvester harvester)
        {
            _harvester = harvester;
            _logger = logger;
            _newClubSeasonAssociationBus = newClubSeasonBus;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("SquadRegistrationExtractor.Svc is registering to new season participant events...");
            _harvester.CookieString = await CookieInitialiser.GetCookieFromRootDirectives();
            _newClubSeasonAssociationBus.RecieveEvents(ExceptionReceivedHandler, ProcessMessagesAsync);
            _logger.LogDebug("SquadRegistrationExtractor.Svc is now listening for new season participant events...");
            await Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("SquadRegistrationExtractor.Svc is closing...");
            await _newClubSeasonAssociationBus.Close();
            _logger.LogDebug("SquadRegistrationExtractor.Svc has successfully shut down...");
        }

        static async Task ProcessMessagesAsync(IBusEvent message, CancellationToken c)
        {
            var payload = Encoding.UTF8.GetString(message.Body);
            _logger.LogDebug($"Received message: Body:{payload}");

            dynamic payloadvalues = JsonConvert.DeserializeObject<ExpandoObject>(payload);
            _harvester.ClubCode = payloadvalues.ClubCode;
            _harvester.SeasonCode = payloadvalues.StageCode;
            _harvester.StageCode = payloadvalues.SeasonCode;
            await _harvester.Execute();
            await _newClubSeasonAssociationBus.CompleteEvent(message.LockToken);
        }

        static Task ExceptionReceivedHandler(IExceptionArgs exceptionReceivedEventArgs)
        {
            _logger.LogDebug($"Message handler encountered an exception {exceptionReceivedEventArgs.Exception}.");
            var context = exceptionReceivedEventArgs.Exception;
            _logger.LogDebug("Exception context for troubleshooting:");
            _logger.LogDebug($"- Message: {context.Message}");
            _logger.LogDebug($"- Stack: {context.StackTrace}");
            _logger.LogDebug($"- Source: {context.Source}");
            return Task.CompletedTask;
        }

        // scratch code to manually invoke new season - invoke from startasync to debug without bus
        //static BusEventBase BuildNewClubSeasonAssociationEvent(string clubcode, string stagecode, string seasoncode)
        //{
        //    dynamic jsonpayload = new ExpandoObject();
        //    jsonpayload.SeasonCode = seasoncode;
        //    jsonpayload.StageCode = stagecode;
        //    jsonpayload.ClubCode = clubcode;
        //    var payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(jsonpayload));
        //    return new BusEventBase(payload);
        //}
    }
}
