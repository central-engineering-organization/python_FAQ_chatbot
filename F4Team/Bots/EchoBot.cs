// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio EchoBot v4.9.2

using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.AI.QnA;
using Microsoft.Bot.Builder.Azure;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace F4Team.Bots
{
    public class EchoBot : ActivityHandler
    {
        protected readonly BotState ConversationState;
        protected readonly BotState UserState;
        public static CosmosDbPartitionedStorage query;
        public static IConfiguration Configuration;
        public CancellationToken cancellationToken { get; private set; }

        /// <summary>
        /// CosmosDB로 부터 값을 가져오는 데 사용되는 클래스에 해당합니다.
        /// </summary>
        public class FunctionItem : IStoreItem
        {
            public string name { get; set; }
            public string description { get; set; }
            public string ETag { get; set; } = "*";
        }

        /// <summary>
        /// 챗봇의 QnAMaker에 해당합니다.
        /// </summary>
        public QnAMaker EchoBotQnA { get; private set; }

        /// <summary>
        /// 챗봇의 생성자에 해당합니다.
        /// </summary>
        /// <param name="conversationState"> 대화의 상태 정보를 가지고 있습니다. </param>
        /// <param name="userState"> 사용자의 상태 정보를 가지고 있습니다. </param>
        /// <param name="endpoint"> QnAMaker의 EndPoint 정보를 가지고 있습니다. </param>
        public EchoBot(ConversationState conversationState, UserState userState, QnAMakerEndpoint endpoint)
        {
            EchoBotQnA = new QnAMaker(endpoint);
            ConversationState = conversationState;
            UserState = userState;
        }

        public class WelcomeUserState
        {
            // Gets or sets whether the user has been welcomed in the conversation.
            public bool DidBotWelcomeUser { get; set; } = false;
        }

#if DATABASE_WRITE_TEST
        private async Task writeDataToDb(FunctionItem[] functionItems)
        {
            if (query == null) { Console.WriteLine("NULL"); }
            var changes = new Dictionary<string, object>();
            changes.Add("function_list", functionItems);
            await query.WriteAsync(changes, cancellationToken);
        }
#endif

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var welcomeUserStateAccessor = UserState.CreateProperty<WelcomeUserState>(nameof(WelcomeUserState));
            var didBotWelcomeUser = await welcomeUserStateAccessor.GetAsync(turnContext, () => new WelcomeUserState());

            if (didBotWelcomeUser.DidBotWelcomeUser == false)
            {
                didBotWelcomeUser.DidBotWelcomeUser = true;
                var welcomeText = $"어서오세요! 파이썬 관련 내용을 알려드립니다.";
                await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText, welcomeText), cancellationToken);
            }
            else
            {
                await base.OnTurnAsync(turnContext, cancellationToken);
            }

            // Save any state changes.
            await UserState.SaveChangesAsync(turnContext);
        }

        private async Task<JObject> makeRequest(string predictionKey, string predictionEndpoint, string appId, string utterance)
        {
            var client = new HttpClient();
            var queryString = HttpUtility.ParseQueryString(string.Empty);

            // The request header contains your subscription key
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", predictionKey);

            // The "q" parameter contains the utterance to send to LUIS
            queryString["query"] = utterance;

            // These optional request parameters are set to their default values
            queryString["verbose"] = "true";
            queryString["show-all-intents"] = "true";
            queryString["staging"] = "false";
            queryString["timezoneOffset"] = "0";

            var predictionEndpointUri = String.Format("{0}luis/prediction/v3.0/apps/{1}/slots/production/predict?{2}", predictionEndpoint, appId, queryString);

            var response = await client.GetAsync(predictionEndpointUri);

            var strResponseContent = await response.Content.ReadAsStringAsync();

            return JObject.Parse(strResponseContent);
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            string replyText = turnContext.Activity.Text;
            var qnaResults = await EchoBotQnA.GetAnswersAsync(turnContext);

            if (!qnaResults.Any())
            {
                string predictionKey = Configuration.GetValue<string>("LuisPredictionKey");
                string predictionEndPoint = Configuration.GetValue<string>("LuisPredictionEndPoint");
                string appId = Configuration.GetValue<string>("LuisId");
                JObject json = await makeRequest(predictionKey, predictionEndPoint, appId, turnContext.Activity.Text);
                turnContext.Activity.Text = json["prediction"].Value<string>("topIntent");
                if (turnContext.Activity.Text != "None")
                {
                    qnaResults = await EchoBotQnA.GetAnswersAsync(turnContext);
                }
            }
            if (!qnaResults.Any())
            {
                string question = "";
                Dictionary<string, string> functionDict = null;
                string[] idList = { "function_list" };
                FunctionItem[] functionItems = null;
                functionItems = query.ReadAsync<FunctionItem[]>(idList).Result?.FirstOrDefault().Value;
                if (!(functionItems is null))
                {
                    functionDict = new Dictionary<string, string>();
                    functionDict = functionItems.ToDictionary(x => x.name, x => x.description);
                }

                if (!(functionDict is null))
                {
                    string str = replyText;
                    str = Regex.Replace(str, @"[^a-zA-Z_]", "");
                    string[] textList = str.Split(" ");
                    foreach (string key in functionDict.Keys)
                    {
                        if (textList.Any(text => text == key))
                        {
                            question = key;
                            break;
                        }
                    }
                }

                if (question.Length > 0)
                {
                    replyText = functionDict[question] as string;
                }
                else
                {
                    replyText = $"제 정보망은 가지고 있지 않네요.(함수의 경우 정확성 향상을 위해 함수 전후로 공백을 부여해주세요!) ==> {replyText}";
                }
                await turnContext.SendActivityAsync(MessageFactory.Text(replyText, replyText), cancellationToken);
            }
            else
            {
                replyText = qnaResults.First().Answer;
                if (qnaResults.First().Context.Prompts.Length > 0)
                {
                    var card = new HeroCard
                    {
                        Text = replyText,
                        Buttons = new List<CardAction>(),
                    };
                    foreach (var prompt in qnaResults.First().Context.Prompts)
                    {
                        card.Buttons.Add(new CardAction(ActionTypes.ImBack, title: prompt.DisplayText, value: prompt.DisplayText));
                    }
                    var reply = MessageFactory.Attachment(card.ToAttachment());
                    await turnContext.SendActivityAsync(reply, cancellationToken);
                }
                else
                {
                    var reply = MessageFactory.Text(replyText, replyText);
                    await turnContext.SendActivityAsync(reply, cancellationToken);
                }
            }
        }

#if DEFAULT_MEMBER_ADD
        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await base.OnTurnAsync(turnContext, cancellationToken);

            // Save any state changes that might have occured during the turn.
            await ConversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await UserState.SaveChangesAsync(turnContext, false, cancellationToken);
        }


        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    var welcomeText = $"어서오세요! 파이썬 관련 내용을 알려드립니다.";
                    await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText, welcomeText), cancellationToken);
                }
            }
        }
#endif
    }
}
