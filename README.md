# 파이썬 레퍼런스 챗봇

## 개  요

이 챗봇은 파이썬의 레퍼런스를 레퍼런스 페이지에 접근해서 찾지 않고, 자연어만으로 조회를 하는 것을 목적으로 만들어졌습니다.

## 구  현

### `Startup.cs`

`Startup.cs`는 MS의 Bot Builder V4 SDK를 기반으로 제작된 챗봇의 초기화를 담당하는 부분에 해당합니다. 여기서 대부분은 SDK에서 봇을 초기화 할 때 자동으로 생성되게 됩니다.

다만 여기서 주의해야 할 부분은 `EchoBot.query`로 시작하는 52번째 줄에 있습니다. 이 줄은 MS의 Cosmos DB를 설정하는 부분으로 이 부분이 없는 경우에는 차후 함수를 정상적으로 찾지 못하는 문제가 발생할 수 있습니다.

설정은 아래와 같이 진행하게 됩니다. 유의 사항으로는 CosmosDbPartitionedStorageOptions 뒤에 괄호가 아니라 바로 중괄호(\{)가 나타나다는 점입니다. 이는 C99에서 구조체 초기화 방식과 유사하다고 생각하시면 될 것 같습니다.

```csharp
EchoBot.query = new CosmosDbPartitionedStorage(new CosmosDbPartitionedStorageOptions{
                CosmosDbEndpoint = Configuration.GetValue<string>("CosmosDbEndPoint"),
                AuthKey = Configuration.GetValue<string>("AuthKey"),
                DatabaseId = Configuration.GetValue<string>("DatabaseId"),
                ContainerId = Configuration.GetValue<string>("ContainerId"),
                CompatibilityMode = Configuration.GetValue<bool>("CompatibilityMode"),
});
```

그리고 위 코드에서 Configuration의 내용은 `appsettings.json`에 "Key: Value"의 형태로 기입해주시면 됩니다.

### `EchoBot.cs`

`EchoBot.cs`는 사용자의 입력에 따라서  설정을 수행하는 부분에 해당합니다. 여기서 `public static`으로 선언된 내용은 전역적으로 사용되는 내용이므로 되도록이면 `readonly`로 사용해주셔야 합니다.

핵심적인 로직은 `OnMessageActivityAsync`에서 진행되게 됩니다. 저희는 사용자의 질의에 맞는 적절한 답안을 찾기 위해서 아래와 같이 알고리즘을 설계했습니다.

```bash
INPUT: Question
OUTPUT: Answer

Answer = CosmosDB(Question) -- (1)
IF Answer is not exist
	Answer = QnAMaker(Question) -- (2)
	IF Answer is not exist
		Answer = QnAMaker(LUIS(Question)) -- (3)
	ENDIF
ENDIF
```

각각의 번호가 설명하는 내용은 아래와 같습니다.

- (1): 질의문에 함수를 포함하는 경우에는 해당 DB에서 해당 함수를 찾아서 그에 대한 설명을 바로 보여주도록 합니다.
- (2): 만약에 DB에 정보가 없는 경우에는 MS의 Azure에서 지원하는 QnAMaker 모듈에 질의문을 보내어 답을 확인해보도록 합니다.
- (3): QnAMaker에서도 답을 찾지 못하는 경우에는 LUIS로 해당 질의문을 정규화 시켜서 QnAMaker로 다시 보내보도록 합니다.

(1), (2), (3) 순서로 찾아가다가 답을 찾은 경우에는 질의문에 맞는 답을 반환을 할 것이고, 그렇지 않은 경우에는 "찾을 수 없다"는 알림 메시지를 보내도록 합니다.

추가로 `EchoBot.cs`에서 구현된 세부 모듈은 아래에 기반하여 제작을 했습니다.

#### QnAMaker

QnAMaker의 경우에는 [링크](https://docs.microsoft.com/ko-kr/azure/bot-service/bot-builder-howto-qna?view=azure-bot-service-4.0&tabs=cs) 정보에 기반하여 질의를 수행할 수 있게 제작을 했습니다.
그리고 카드의 경우에는 [Hero Card](https://docs.microsoft.com/ko-kr/azure/bot-service/bot-builder-howto-add-media-attachments?view=azure-bot-service-4.0&tabs=csharp#send-a-hero-card)를 QnAMaker가 반환한 결과의 `Context.Prompts`의 갯수를 확인하고, 확인한 만큼 생성하도록 제작했습니다.

#### LUIS

LUIS는 따로 다이얼로그를 제작하는 것이 아니라 [REST API 형태로 접근](https://docs.microsoft.com/ko-kr/azure/cognitive-services/luis/luis-get-started-get-intent-from-rest?pivots=programming-language-csharp)하여 QnAMaker와 함께 사용할 수 있도록 제작했습니다.

#### CosmosDB

CosmosDB의 접근은 ReadAsync와 WriteAsync에 기반하여 데이터를 가져올 수 있도록 개발했습니다.

## 결  과

샘플은 [링크](https://central-engineering-organization.github.io/python-ref-chat-bot/)에서 확인하실 수 있습니다.

