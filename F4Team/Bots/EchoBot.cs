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
using Microsoft.AspNetCore.Connections.Features;
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

        /// <summary>
        /// Welcome이 정상 동작할 수 있도록 Welcome에 관련한 상태를 가집니다.
        /// </summary>
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

        /// <summary>
        /// 시스템에 임의의 사용자가 접근을 하게되면 welcome 메시지를 띄우도록 합니다.
        /// </summary>
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

            // 사용자의 상태를 저장합니다.
            await UserState.SaveChangesAsync(turnContext);
        }

        /// <summary>
        /// LUIS에 utterance를 통과시켜 utterance에서 묻는 모호한 질문을
        /// QnAMaker가 이해하기 쉬운 형태의 질문으로 변경하는 역할을 합니다.
        /// </summary>
        /// <param name="predictionKey"> LUIS 접속 관련 키 값입니다. </param>
        /// <param name="predictionEndpoint"> LUIS 접속 관련 키 값입니다. </param>
        /// <param name="appId"> LUIS 접속 관련 키 값입니다. </param>
        /// <param name="utterance"> LUIS 접속 관련 키 값입니다. </param>
        /// <returns> LUIS로부터 받은 JSON 데이터를 반환해줍니다. </returns>
        private async Task<JObject> makeRequest(string predictionKey, string predictionEndpoint, string appId, string utterance)
        {
            var client = new HttpClient();
            var queryString = HttpUtility.ParseQueryString(string.Empty);

            // 구독 키를 가지는 헤더를 만들도록 합니다.
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", predictionKey);

            // LUIS에 모호한 질의문을 넣어주는 역할을 합니다.
            queryString["query"] = utterance;

            // 추가적인 설정입니다. 필요한 경우 사용하면 될 것 같습니다.
            queryString["verbose"] = "true";
            queryString["show-all-intents"] = "true";
            queryString["staging"] = "false";
            queryString["timezoneOffset"] = "0";

            // REST 형태의 요청 URI입니다.
            var predictionEndpointUri = String.Format("{0}luis/prediction/v3.0/apps/{1}/slots/production/predict?{2}", predictionEndpoint, appId, queryString);

            // 요청과 요청에 대한 결과를 받도록 합니다.
            var response = await client.GetAsync(predictionEndpointUri);
            var strResponseContent = await response.Content.ReadAsStringAsync();

            return JObject.Parse(strResponseContent);
        }

        /// <summary>
        /// 실제 질의 응답 과정이 처리되게 됩니다.
        /// </summary>
        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            IMessageActivity reply;
            string replyText = turnContext.Activity.Text;
            var functionDict = GenerateFunctionDictionFromDB();
            string question = FindFunctionQuery(replyText, functionDict);
            if (question.Length > 0) // 데이터베이스에서 답을 찾은 경우
            {
                reply = FunctionInfoReply(question, functionDict);
            }
            else // 데이터베이스에서 답을 찾지 못한 경우
            {
                reply = await QnAMakerReply(turnContext, replyText);
            }
            await turnContext.SendActivityAsync(reply, cancellationToken);
        }

        /// <summary>
        /// 함수명과 함수설명으로 이뤄진 Key-Value DB를 참조하여 딕셔너리 객체를 생성합니다.
        /// </summary>
        /// <returns>생성된 함수 딕셔너리 객체를 반환합니다.</returns>
        private static Dictionary<string, string> GenerateFunctionDictionFromDB()
        {
            Dictionary<string, string> functionDict = null;
            string[] idList = { "function_list" }; // 테이블 이름 정보가 들어갑니다.
            FunctionItem[] functionItems = null;
            // 데이터베이스에서 데이터를 조회를 하도록 합니다.
            functionItems = query.ReadAsync<FunctionItem[]>(idList).Result?.FirstOrDefault().Value;
            if (!(functionItems is null)) // 데이터베이스에 데이터가 있는 경우
            {
                functionDict = new Dictionary<string, string>();
                functionDict = functionItems.ToDictionary(x => x.name, x => x.description);
            }
            return functionDict;
        }

        /// <summary>
        /// 주워진 쿼리에서 함수명이 있는지 확인하고 이를 반환합니다.
        /// </summary>
        /// <param name="queryText">비형식적인 쿼리를 받습니다.</param>
        /// <param name="functionDict">존재 여부를 확인하기 위해 함수 딕셔너리를 받습니다.</param>
        /// <returns>
        /// 함수 딕셔너리에 존재하는 함수명일 경우, 함수명을 반환합니다.
        /// 이 외의 경우, 공백 문자를 반환합니다.
        /// </returns>
        private static string FindFunctionQuery(string queryText, Dictionary<string, string> functionDict)
        {
            if (functionDict is null) // 데이터로부터 딕셔너리를 만들지 않은 경우
            {
                return "";
            }

            string str = queryText;
            str = Regex.Replace(str, @"[^a-zA-Z_.]", "");
            string[] textList = str.Split(" ");
            foreach (string key in functionDict.Keys)
            {
                if (textList.Any(text => text == key))
                {
                    return key;
                }
            }
            return "";
        }

        /// <summary>
        /// 함수명을 통해 얻은 함수정보를 통해 Reply 생성합니다.
        /// </summary>
        /// <param name="functionName">함수명을 받습니다.</param>
        /// <param name="functionDict">함수정보를 얻기 위해 함수 딕셔너리를 받습니다.</param>
        /// <returns>함수명에 관한 함수정보가 담긴 Reply를 반환합니다.</returns>
        private static IMessageActivity FunctionInfoReply(string functionName, Dictionary<string, string> functionDict)
        {
            var replyText = functionDict[functionName] as string;
            return MessageFactory.Text(replyText, replyText);
        }

        /// <summary>
        /// QnA Maker를 통해 얻은 Reply를 생성합니다.
        /// </summary>
        /// <returns>QnA Maker를 통해 얻은 Reply를 반환합니다.</returns>
        private async Task<IMessageActivity> QnAMakerReply(ITurnContext<IMessageActivity> turnContext, string replyText)
        {
            var qnaResults = await EchoBotQnA.GetAnswersAsync(turnContext);

            if (!qnaResults.Any()) // QnAMaker가 이해하기 힘든 형태의 질의문의 경우
            {
                qnaResults = await ClarifyUsingLuis(turnContext, qnaResults);
            }

            if (qnaResults.Any()) // QnAMaker가 답을 아는 경우입니다.
            {
                replyText = qnaResults.First().Answer; // 가장 유사도가 높은 답을 가져옵니다.
                MatchCollection functionNotationMatches = Regex.Matches(replyText, "\\{\\{[ ]*[a-zA-Z0-9_\\.\\(\\)]*[ ]*\\}\\}");
                if (functionNotationMatches.Count > 0) // 함수 버튼을 만들어야 하는 지 확인해보도록 합니다.
                {
                    return FunctionListReply(functionNotationMatches);
                }
                else if (qnaResults.First().Context.Prompts.Length > 0) // 버튼을 만들어야 하는 지 확인해보도록 합니다.
                {
                    return GeneralReplyWithButton(replyText, qnaResults);
                }
                else // 버튼이 필요 없는 경우에 해당합니다.
                {
                    return MessageFactory.Text(replyText, replyText);
                }
            }
            else // QnAMaker로는 답변을 찾을 수 없는 경우에 사용됩니다.
            {
                return NoAnswerReply(replyText);
            }
        }

        /// <summary>
        /// 불투명한 퀴리문을 LUIS를 통해 명확하도록 개선해줍니다.
        /// </summary>
        /// <param name="queryResult">불투명한 쿼리문을 받습니다.</param>
        /// <returns>
        /// 개선된 쿼리문을 반환합니다.
        /// 개선이 불가한 경우, 기존 퀴리를 반환합니다.
        /// </returns>
        private async Task<QueryResult[]> ClarifyUsingLuis(ITurnContext<IMessageActivity> turnContext, QueryResult[] queryResults)
        {
            string predictionKey = Configuration.GetValue<string>("LuisPredictionKey");
            string predictionEndPoint = Configuration.GetValue<string>("LuisPredictionEndPoint");
            string appId = Configuration.GetValue<string>("LuisId");
            JObject json = await makeRequest(predictionKey, predictionEndPoint, appId, turnContext.Activity.Text);
            var topIntent = json["prediction"].Value<string>("topIntent");
            var prediction = json["prediction"]["intents"][topIntent].Value<Double>("score");
            turnContext.Activity.Text = topIntent;
            if (turnContext.Activity.Text != "None" && prediction > 0.6) // "None"은 LUIS도 모르는 질의문을 의미합니다.
            {
                queryResults = await EchoBotQnA.GetAnswersAsync(turnContext);
            }
            return queryResults;
        }

        /// <summary>
        /// 버튼이 있는 형식의 Reply를 생성합니다.
        /// </summary>
        /// <returns>버튼식 Reply를 반환합니다.</returns>
        private static IMessageActivity GeneralReplyWithButton(string replyText, QueryResult[] queryResult)
        {
            var card = new HeroCard
            {
                Text = replyText,
                Buttons = new List<CardAction>(),
            };
            foreach (var prompt in queryResult.First().Context.Prompts)
            {
                card.Buttons.Add(new CardAction(ActionTypes.ImBack, title: prompt.DisplayText, value: prompt.DisplayText));
            }
            return MessageFactory.Attachment(card.ToAttachment());
        }

        /// <summary>
        /// 함수 리스트가 있는 형식의 Reply를 생성합니다.
        /// </summary>
        /// <param name="matches">함수 리스트를 받습니다.</param>
        /// <returns>함수 리스트형 Reply를 반환합니다.</returns>
        private static IMessageActivity FunctionListReply(MatchCollection matches)
        {
            var card = new HeroCard
            {
                Text = "요청하신 내용과 관계있는 결과입니다.",
                Buttons = new List<CardAction>(),
            };
            foreach (Match match in matches)
            {
                string value = match.Value.Replace("{", "").Replace("}", "").Trim();
                string title = value + "()";
                card.Buttons.Add(new CardAction(ActionTypes.ImBack, title: title, value: value));
            }
            return MessageFactory.Attachment(card.ToAttachment());
        }

        /// <summary>
        /// 답변 불가 형식의 Reply를 생성합니다.
        /// </summary>
        /// <returns>답변 불가식 Reply를 반환합니다.</returns>
        private static IMessageActivity NoAnswerReply(string replyText)
        {
            replyText = $"제 정보망은 가지고 있지 않네요.(함수의 경우 정확성 향상을 위해 함수 전후로 공백을 부여해주세요!) ==> {replyText}";
            return MessageFactory.Text(replyText, replyText);
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
