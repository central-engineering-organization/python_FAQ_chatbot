// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio EchoBot v4.9.2

using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.AI.QnA;
using Microsoft.Bot.Builder.Azure;
using Microsoft.Bot.Schema;

namespace F4Team.Bots
{
    public class EchoBot : ActivityHandler
    {
        public static CosmosDbPartitionedStorage query;

        private BotState _userState;

        public CancellationToken cancellationToken { get; private set; }

        public class FunctionItem : IStoreItem
        {
            public string name { get; set; }
            public string description { get; set; }
            public string ETag { get; set; } = "*";
        }

        public QnAMaker EchoBotQnA { get; private set; }

        public EchoBot(QnAMakerEndpoint endpoint, UserState userState)
        {
            EchoBotQnA = new QnAMaker(endpoint);
            _userState = userState;
        }


        private async Task writeDataToDb(FunctionItem[] functionItems)
        {
            if (query == null) { Console.WriteLine("NULL"); }
            var changes = new Dictionary<string, object>();
            changes.Add("function_list", functionItems);
            await query.WriteAsync(changes, cancellationToken);
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var welcomeUserStateAccessor = _userState.CreateProperty<WelcomeUserState>(nameof(WelcomeUserState));
            var didBotWelcomeUser = await welcomeUserStateAccessor.GetAsync(turnContext, () => new WelcomeUserState());

            if (didBotWelcomeUser.DidBotWelcomeUser == false)
            {
                didBotWelcomeUser.DidBotWelcomeUser = true;
                var welcomeText = $"Welcome Test";
                await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText, welcomeText), cancellationToken);
            }
            else
            {
                await base.OnTurnAsync(turnContext, cancellationToken);
            }

            // Save any state changes.
            await _userState.SaveChangesAsync(turnContext);
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            string replyText;
            var qnaResults = await EchoBotQnA.GetAnswersAsync(turnContext);
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
                    string str = turnContext.Activity.Text;
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
                    replyText = $"제 정보망은 가지고 있지 않네요.(함수의 경우 정확성 향상을 위해 함수 전후로 공백을 부여해주세요!) ==> {turnContext.Activity.Text}";
                }
            } else
            {
                replyText = qnaResults.First().Answer;
            }
            await turnContext.SendActivityAsync(MessageFactory.Text(replyText, replyText), cancellationToken);
        }

        //protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        //{
        //    foreach (var member in membersAdded)
        //    {
        //        if (member.Id != turnContext.Activity.Recipient.Id)
        //        {
        //            var welcomeText = $"어서오세요! 파이썬 관련 내용을 알려드립니다.";
        //            await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText, welcomeText), cancellationToken);
        //        }
        //    }
        //}
    }
}
