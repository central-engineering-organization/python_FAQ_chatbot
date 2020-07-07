// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio EchoBot v4.9.2

using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace F4Team.Bots
{
    public class EchoBot : ActivityHandler
    {
        private Hashtable table;
        public EchoBot()
        {
            table = new Hashtable();
            table.Add("안녕", "안녕하세요. 좋은 하루 입니다.");
            table.Add("버전", "현재 파이썬 버전은 3입니다.");
            table.Add("레퍼런스", "https://docs.python.org/ko/3/reference/index.html 참조해주세요.");
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            string replyText;
            string question = "";

            foreach (string key in table.Keys)
            {
                if (turnContext.Activity.Text.Contains(key))
                {
                    question = key;
                }
            }

            if (question.Length > 0)
            {
                replyText = table[question] as string;
            }
            else
            {
                replyText = $"현재 등록되지 않은 질의문: {turnContext.Activity.Text}";
            }
            await turnContext.SendActivityAsync(MessageFactory.Text(replyText, replyText), cancellationToken);
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
    }
}
