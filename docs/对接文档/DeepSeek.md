# Deepseek对接文档

DeepSeek是一家优秀的国产大模型，其输入上下文高达`1M`，非常适合大范围输出
 
## API文档

[在线对接文档](https://api-docs.deepseek.com/zh-cn/api/create-chat-completion)

## 本地对接文档简要说明

### 注意事项

模型使用：`deepseek-v4-flash` 和 `deepseek-v4-pro` 对比下质量，首选flash  

max_tokens设置为384000

模型输入上下文高达1M，可以支持完整大文本输入

### 样例代码

```csharp

var client = new HttpClient();
var request = new HttpRequestMessage(HttpMethod.Post, "https://api.deepseek.com/chat/completions");
request.Headers.Add("Accept", "application/json");
request.Headers.Add("Authorization", "Bearer <TOKEN>");
var content = new StringContent("{\n  \"messages\": [\n    {\n      \"content\": \"You are a helpful assistant\",\n      \"role\": \"system\"\n    },\n    {\n      \"content\": \"Hi\",\n      \"role\": \"user\"\n    }\n  ],\n  \"model\": \"deepseek-v4-pro\",\n  \"thinking\": {\n    \"type\": \"enabled\"\n  },\n  \"reasoning_effort\": \"high\",\n  \"max_tokens\": 4096,\n  \"response_format\": {\n    \"type\": \"text\"\n  },\n  \"stop\": null,\n  \"stream\": false,\n  \"stream_options\": null,\n  \"temperature\": 1,\n  \"top_p\": 1,\n  \"tools\": null,\n  \"tool_choice\": \"none\",\n  \"logprobs\": false,\n  \"top_logprobs\": null\n}", null, "application/json");
request.Content = content;
var response = await client.SendAsync(request);
response.EnsureSuccessStatusCode();
Console.WriteLine(await response.Content.ReadAsStringAsync());

```

### 请求样例

```json

{
  "messages": [
    {
      "content": "You are a helpful assistant",
      "role": "system"
    },
    {
      "content": "Hi",
      "role": "user"
    }
  ],
  "model": "deepseek-v4-pro",
  "thinking": {
    "type": "enabled"
  },
  "reasoning_effort": "high",
  "max_tokens": 4096,
  "response_format": {
    "type": "text"
  },
  "stop": null,
  "stream": false,
  "stream_options": null,
  "temperature": 1,
  "top_p": 1,
  "tools": null,
  "tool_choice": "none",
  "logprobs": false,
  "top_logprobs": null
}

```

### 返回样例（流式）

```json

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"role":"assistant","content":null,"reasoning_content":""},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":"We"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" are"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" asked"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":":"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" \""},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":"Hi"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":"\""},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" as"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" the"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" user"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" message"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":"."},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" The"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" assistant"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" should"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" respond"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":"."},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" This"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" is"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" a"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" simple"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" greeting"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":"."},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" The"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" assistant"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" should"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" reply"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" in"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" a"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" friendly"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" manner"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":"."},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" No"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" special"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" instructions"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":"."},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" Just"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":" respond"},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":null,"reasoning_content":"."},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":"Hello","reasoning_content":null},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":"!","reasoning_content":null},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":" How","reasoning_content":null},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":" can","reasoning_content":null},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":" I","reasoning_content":null},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":" help","reasoning_content":null},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":" you","reasoning_content":null},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":" today","reasoning_content":null},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":"?","reasoning_content":null},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":" 😊","reasoning_content":null},"logprobs":null,"finish_reason":null}]}

data: {"id":"f7fdd783-141b-4fe2-b846-17abd2289a01","object":"chat.completion.chunk","created":1779383974,"model":"deepseek-v4-pro","system_fingerprint":"fp_9954b31ca7_prod0820_fp8_kvcache_20260402","choices":[{"index":0,"delta":{"content":"","reasoning_content":null},"logprobs":null,"finish_reason":"stop"}],"usage":{"prompt_tokens":10,"completion_tokens":51,"total_tokens":61,"prompt_tokens_details":{"cached_tokens":0},"completion_tokens_details":{"reasoning_tokens":39},"prompt_cache_hit_tokens":0,"prompt_cache_miss_tokens":10}}

data: [DONE]


```

### 返回样例（非流式）

```json

{
  "id": "c4caca64-cd75-4b15-994e-ded7fac71561",
  "object": "chat.completion",
  "created": 1779384066,
  "model": "deepseek-v4-pro",
  "choices": [
    {
      "index": 0,
      "message": {
        "role": "assistant",
        "content": "Hello! How can I help you today?",
        "reasoning_content": "We are asked: \"Hi\" - just a friendly greeting. The assistant should respond in a friendly manner, keeping it concise and natural. Since we are an AI assistant, we should respond accordingly. No special instructions, just a simple greeting."
      },
      "logprobs": null,
      "finish_reason": "stop"
    }
  ],
  "usage": {
    "prompt_tokens": 10,
    "completion_tokens": 59,
    "total_tokens": 69,
    "prompt_tokens_details": {
      "cached_tokens": 0
    },
    "completion_tokens_details": {
      "reasoning_tokens": 49
    },
    "prompt_cache_hit_tokens": 0,
    "prompt_cache_miss_tokens": 10
  },
  "system_fingerprint": "fp_9954b31ca7_prod0820_fp8_kvcache_20260402"
}

```