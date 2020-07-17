# 파이썬 레퍼런스 챗봇

## 시스템 전체

이 챗봇은 파이썬의 레퍼런스를 레퍼런스 페이지에 접근해서 찾지 않고, 자연어만으로 조회를 하는 것을 목적으로 만들어졌습니다.

챗봇의 초기화는 `Startup.cs`에서 벌어지고 실제 동작은 `EchoBot.cs`에서 수행됩니다.

`EchoBot.cs`에서 답변을 수행하는 `OnMessageActivityAsync`의 간략한 동작은 아래와 같습니다.
```bash
INPUT: Question
OUTPUT: Answer

Answer = QnAMaker(Question)
IF Answer == None --- (1)
    FormattedQuestion = LUIS(Question)
    IF FormattedQuestion == None --- (2)
        Answer = None
    ELSE
        Answer = QnAMaker(FormattedQuestion)
    ENDIF

    IF Answer == None --- (3)
        Answer = CosmosDb(Question)
    ENDIF
ENDIF
```

각각의 번호가 설명하는 내용은 아래와 같습니다.

- (1): 사용자로부터 받은 질의문은 QnAMaker로 바로 통과시켜보도록 합니다. 만약, 통과된다면 사용자는 Answer를 결과로 받아보게 됩니다.
- (2): 만약 그렇지 않은 경우 LUIS에서 비정형 질의문을 정형화 시켜주도록 합니다. 이때, 정형화에 실패하는 경우 (3)으로 가게 됩니다. 성공하는 경우에는 정형화된 질의문을 QnAMaker에 통과시키도록 합니다. 통과되는 경우에는 사용자는 Answer를 결과로 받아보게되나, 통과되지 않는 경우에는 (3)으로 진행합니다.
- (3): 마지막으로 데이터베이스에서 질의문이 포함하는 단어를 가진 내용이 있는 지를 확인하고, 있는 경우에는 해당 값을 Answer로 반환하고 없는 경우에는 관련 질의문에 대한 답변이 없다는 말을 반환합니다.


## 세부 모듈의 구축 방법

### QnAMaker

QnAMaker의 경우에는 [링크](https://docs.microsoft.com/ko-kr/azure/bot-service/bot-builder-howto-qna?view=azure-bot-service-4.0&tabs=cs) 정보에 기반하여 질의를 수행할 수 있게 제작을 했습니다.
그리고 카드의 경우에는 [Hero Card](https://docs.microsoft.com/ko-kr/azure/bot-service/bot-builder-howto-add-media-attachments?view=azure-bot-service-4.0&tabs=csharp#send-a-hero-card)를 QnAMaker가 반환한 결과의 `Context.Prompts`의 갯수를 확인하고, 확인한 만큼 생성하도록 제작했습니다.

### LUIS

LUIS는 따로 다이얼로그를 제작하는 것이 아니라 [REST API 형태로 접근](https://docs.microsoft.com/ko-kr/azure/cognitive-services/luis/luis-get-started-get-intent-from-rest?pivots=programming-language-csharp)하여 QnAMaker와 함께 사용할 수 있도록 제작했습니다.

### CosmosDB

CosmosDB의 접근은 ReadAsync와 WriteAsync에 기반하여 데이터를 가져올 수 있도록 개발했습니다.
