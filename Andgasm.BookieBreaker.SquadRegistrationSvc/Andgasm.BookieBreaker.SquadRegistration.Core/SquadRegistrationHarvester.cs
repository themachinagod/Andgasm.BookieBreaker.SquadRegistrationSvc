﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using HtmlAgilityPack;
using Andgasm.BookieBreaker.Harvest;
using System.Dynamic;
using Andgasm.Http;
using Microsoft.Extensions.Configuration;

namespace Andgasm.BookieBreaker.SquadRegistration.Core
{
    public class SquadRegistrationHarvester : DataHarvest
    {
        #region Fields
        ILogger<SquadRegistrationHarvester> _logger;
        ApiSettings _apisettings;

        string _playersapiroot;
        string _registrationsApiPath;
        #endregion

        #region Properties
        public string StageCode { get; set; }
        public string SeasonCode { get; set; }
        public string ClubCode { get; set; }
        #endregion

        #region Contructors
        public SquadRegistrationHarvester(ApiSettings settings, ILogger<SquadRegistrationHarvester> logger, HarvestRequestManager requestmanager)
        {
            _logger = logger;
            _requestmanager = requestmanager;

            _playersapiroot = settings.PlayersDbApiRootKey;
            _registrationsApiPath = settings.PlayerSquadRegistrationsApiPath;
            _apisettings = settings;
        }
        #endregion

        #region Execution Operations
        public override bool CanExecute()
        {
            if (!base.CanExecute()) return false;
            if (string.IsNullOrWhiteSpace(StageCode)) return false;
            if (string.IsNullOrWhiteSpace(SeasonCode)) return false;
            if (string.IsNullOrWhiteSpace(ClubCode)) return false;
            return true;
        }

        public async override Task Execute()
        {
            if (CanExecute())
            {
                _timer.Start();
                var lastmodekey = await DetermineLastModeKey();
                HtmlDocument responsedoc = await ExecuteRequest(lastmodekey);
                if (responsedoc != null)
                {
                    var players = new List<ExpandoObject>();
                    foreach (var pl in ParsePlayersFromResponse(responsedoc))
                    {
                        players.Add(CreateSquadPlayer(pl));
                    };
                    await HttpRequestFactory.Post(players, _playersapiroot, _registrationsApiPath);
                    _logger.LogDebug(string.Format("Stored new player data to database for club key '{0}' and season key '{1}'", ClubCode, SeasonCode));
                }
                else
                {
                    _logger.LogDebug(string.Format("Failed to store & commit player squad registration for season '{0}' in data store.", SeasonCode));
                }
                HarvestHelper.FinaliseTimer(_timer);
            };
        }
        #endregion

        #region Entity Creation Helpers
        private string CreateRequestUrl()
        {
            return string.Format(WhoScoredConstants.PlayerStatisticsFeedUrl, StageCode, ClubCode);
        }

        private string CreateRefererUrl()
        {
            return string.Format(WhoScoredConstants.ClubsUrl, ClubCode);
        }

        private async Task<string> DetermineLastModeKey()
        {
            var referer = CreateRefererUrl();
            var ctx = HarvestHelper.ConstructRequestContext(null, "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8", WhoScoredConstants.RootUrl,
                                                            @"euconsent=BOVu1IfOVu1IfABABAENBE-AAAAcd7_______9______9uz_Gv_r_f__33e8_39v_h_7_-___m_-33d4-_1vV11yPg1urfIr1NpjQ6OGsA; visid_incap_774904=/t5l9RDSS5C89/fvHh+pv01xTFsAAAAASUIPAAAAAACAXZWHAbkPzqLpwgKF6J+VxSsT1yPC6hpU; incap_ses_867_774904=D9LZV2Cj1UhtRXUXbzQIDL0Bx1sAAAAA8B31aB37Vng6nilidRNF+A==",
                                                            "en-GB,en;q=0.9,en-US;q=0.8,th;q=0.7", false, true, true);
            var parentresponse = await _requestmanager.MakeRequest(referer, ctx);
            //var parentresponse = await HarvestHelper.AttemptRequest(referer, "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8",
            //                                                            WhoScoredConstants.RootUrl, null, @"euconsent=BOVu1IfOVu1IfABABAENBE-AAAAcd7_______9______9uz_Gv_r_f__33e8_39v_h_7_-___m_-33d4-_1vV11yPg1urfIr1NpjQ6OGsA; visid_incap_774904=/t5l9RDSS5C89/fvHh+pv01xTFsAAAAASUIPAAAAAACAXZWHAbkPzqLpwgKF6J+VxSsT1yPC6hpU; incap_ses_867_774904=D9LZV2Cj1UhtRXUXbzQIDL0Bx1sAAAAA8B31aB37Vng6nilidRNF+A==",
            //                                                             "en-GB,en;q=0.9,en-US;q=0.8,th;q=0.7", false, _requestmanager, null, true, true);
            if (parentresponse != null)
            {
                return GetLastModeKey(parentresponse.DocumentNode.InnerText);
            }
            return null;
        }

        private async Task<HtmlDocument> ExecuteRequest(string lastmodekey)
        {
            var url = CreateRequestUrl();
            var referer = CreateRefererUrl();
            var ctx = HarvestHelper.ConstructRequestContext(lastmodekey, "application/json,text/javascript,*/*; q=0.01", referer,
                                                           @"euconsent=BOVu1IfOVu1IfABABAENBE-AAAAcd7_______9______9uz_Gv_r_f__33e8_39v_h_7_-___m_-33d4-_1vV11yPg1urfIr1NpjQ6OGsA; visid_incap_774904=/t5l9RDSS5C89/fvHh+pv01xTFsAAAAASUIPAAAAAACAXZWHAbkPzqLpwgKF6J+VxSsT1yPC6hpU; incap_ses_867_774904=D9LZV2Cj1UhtRXUXbzQIDL0Bx1sAAAAA8B31aB37Vng6nilidRNF+A==",
                                                            "en-GB", true, false, false);
            return await _requestmanager.MakeRequest(url, ctx);
            //return await HarvestHelper.AttemptRequest(url, "application/json,text/javascript,*/*; q=0.01", referer, lastmodekey, @"euconsent=BOVu1IfOVu1IfABABAENBE-AAAAcd7_______9______9uz_Gv_r_f__33e8_39v_h_7_-___m_-33d4-_1vV11yPg1urfIr1NpjQ6OGsA; visid_incap_774904=/t5l9RDSS5C89/fvHh+pv01xTFsAAAAASUIPAAAAAACAXZWHAbkPzqLpwgKF6J+VxSsT1yPC6hpU; incap_ses_867_774904=D9LZV2Cj1UhtRXUXbzQIDL0Bx1sAAAAA8B31aB37Vng6nilidRNF+A==",
            //     "en-GB", true, _requestmanager, "playerTableStats");
        }

        private JArray ParsePlayersFromResponse(HtmlDocument response)
        {
            var rawdata = response.DocumentNode.InnerHtml;
            var startIndex = rawdata.IndexOf("{     \"playerTableStats\" : ");
            var endIndex = rawdata.IndexOf(", \"paging\" :");
            rawdata = rawdata.Substring(startIndex + 27, (endIndex - (startIndex + 27)));
            return JsonConvert.DeserializeObject<JArray>(rawdata);
        }

        private ExpandoObject CreateSquadPlayer(JToken playerdata)
        {
            dynamic player = new ExpandoObject();
            player.Name = playerdata["firstName"].ToString();
            player.Surname = playerdata["lastName"].ToString();
            player.CountryKey = playerdata["regionCode"].ToString();
            player.PlayerCode = playerdata["playerId"].ToString();
            player.Height = playerdata["height"].ToString();
            player.Weight = playerdata["weight"].ToString();
            player.Positions = playerdata["playedPositionsShort"].ToString();
            player.ClubKey = ClubCode;
            player.SeasonKey = SeasonCode;
            return player;
        }
        #endregion
    }
}