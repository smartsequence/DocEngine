using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using DocEngine.Models;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;  // ✅ appsettings
using System;

namespace DocEngine.Controllers;

public class HomeController : Controller
{
    private const int PORT = 5163; //定義Port號
    private readonly ILogger<HomeController> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public HomeController(ILogger<HomeController> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(60); // 設置 60 秒超時
        var apiKey = _configuration["OpenAI:ApiKey"];
        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
        _httpClient.BaseAddress = new Uri("https://api.openai.com/v1/");
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Assessment()
    {
        return View();
    }

    [HttpPost]
    public IActionResult SubmitSurvey([FromBody] Dictionary<string, string> data)
    {
        try
        {
            // 將問卷數據存儲在 Session 中（服務端存儲，更安全）
            HttpContext.Session.SetString("SurveyData", JsonSerializer.Serialize(data));
            
            // 同時存儲時間戳
            HttpContext.Session.SetString("SurveyTimestamp", DateTime.Now.ToString("O"));
            
            _logger.LogInformation("Survey data saved to session: {SessionId}", HttpContext.Session.Id);
            
            return Json(new { success = true, message = "Survey submitted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save survey data");
            return Json(new { success = false, message = "Failed to save survey data" });
        }
    }

    // 獲取問卷數據（從 Session）
    [HttpGet]
    public IActionResult GetSurveyData()
    {
        try
        {
            var surveyDataJson = HttpContext.Session.GetString("SurveyData");
            var timestamp = HttpContext.Session.GetString("SurveyTimestamp");
            
            if (string.IsNullOrEmpty(surveyDataJson))
            {
                return Json(new { success = false, message = "No survey data found" });
            }
            
            var surveyData = JsonSerializer.Deserialize<Dictionary<string, string>>(surveyDataJson);
            
            return Json(new 
            { 
                success = true, 
                data = surveyData,
                timestamp = timestamp
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve survey data");
            return Json(new { success = false, message = "Failed to retrieve survey data" });
        }
    }

    // 清除問卷數據
    [HttpPost]
    public IActionResult ClearSurveyData()
    {
        try
        {
            HttpContext.Session.Remove("SurveyData");
            HttpContext.Session.Remove("SurveyTimestamp");
            return Json(new { success = true, message = "Survey data cleared" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear survey data");
            return Json(new { success = false, message = "Failed to clear survey data" });
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [HttpPost]
    public IActionResult GetEcpayFormData()
    {
        var baseUrl = $"http://localhost:{PORT}";  // ✅ 組成基礎網址
        
        var formData = new Dictionary<string, string>
        {
            ["MerchantID"] = "2000132",
            ["MerchantTradeNo"] = "DOC" + DateTime.Now.ToString("yyyyMMddHHmmss"),
            ["MerchantTradeDate"] = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"),
            ["PaymentType"] = "aio",
            ["TotalAmount"] = "2990",
            ["TradeDesc"] = "Doc Engine Report",
            ["ItemName"] = "Risk Assessment Report",
            ["ReturnURL"] = $"{baseUrl}/Home/EcpayReturn",  // ✅ 使用變數
            ["ChoosePayment"] = "Credit",
            ["EncryptType"] = "1",
            ["ClientBackURL"] = $"{baseUrl}/Home/Report",  // ✅ 使用變數
            ["OrderResultURL"] = $"{baseUrl}/Home/EcpayReturn",  // ✅ 使用變數
            ["NeedExtraPaidInfo"] = "N"
        };

        string hashKey = "5294y06JbISpM5x9";
        string hashIV = "v77hoKGq4kWxNNIS";
        
        formData["CheckMacValue"] = GenCheckMacValue(formData, hashKey, hashIV);
        
        return Json(formData);
    }

    private string GenCheckMacValue(Dictionary<string, string> parameters, string hashKey, string hashIV)
    {
        // ✅ 排除 CheckMacValue 欄位
        var sortedParams = parameters
            .Where(x => x.Key != "CheckMacValue")
            .OrderBy(x => x.Key)
            .Select(x => $"{x.Key}={x.Value}");
        
        // ✅ 組合字串
        var checkValue = $"HashKey={hashKey}&{string.Join("&", sortedParams)}&HashIV={hashIV}";
        
        // ✅ URL Encode
        checkValue = System.Web.HttpUtility.UrlEncode(checkValue).ToLower();
        
        // ✅ SHA256 加密
        using var sha256 = SHA256.Create();
        byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(checkValue));
        return BitConverter.ToString(bytes).Replace("-", "").ToUpper();
    }

    public IActionResult EcpayReturn()
    {
        // 綠界跳回成功頁
        return View("Report");
    }

    // 風險改善建議 API（基於 M1-M5 分數）
    [HttpPost]
    public async Task<IActionResult> GeneratePersonalizedAdvice([FromBody] Dictionary<string, string> data)
    {
        try
        {
            var lang = data?.GetValueOrDefault("lang") ?? "zh-TW";
            var m1 = data?.GetValueOrDefault("m1") ?? data?.GetValueOrDefault("M1") ?? "0";
            var m2 = data?.GetValueOrDefault("m2") ?? data?.GetValueOrDefault("M2") ?? "0";
            var m3 = data?.GetValueOrDefault("m3") ?? data?.GetValueOrDefault("M3") ?? "0";
            var m4 = data?.GetValueOrDefault("m4") ?? data?.GetValueOrDefault("M4") ?? "0";
            var m5 = data?.GetValueOrDefault("m5") ?? data?.GetValueOrDefault("M5") ?? "0";

            var isEnglish = lang == "en-US";
            
            // 如果請求英文版本，先檢查 Session 中是否有中文版本
            if (isEnglish)
            {
                var chineseAdvice = HttpContext.Session.GetString("PersonalizedAdvice_zh-TW");
                if (!string.IsNullOrEmpty(chineseAdvice))
                {
                    // 有中文版本，進行翻譯
                    _logger.LogInformation("發現中文版本的風險改善建議，進行翻譯");
                    var translatedAdvice = await TranslateAdviceToEnglish(chineseAdvice);
                    return Json(new { advice = translatedAdvice });
                }
                // 如果沒有中文版本，繼續生成中文版本（然後翻譯）
                _logger.LogInformation("未找到中文版本的風險改善建議，將先生成中文版本");
            }

            // 生成中文版本（優先）
            var systemPrompt = @"你是資深的專案管理和文件風險評估專家。請根據用戶的風險評估分數，提供風險改善建議，並確保建議可執行。

**極重要：請嚴格使用台灣繁體中文正式用語，絕對禁止使用大陸簡體中文用語。這是政府機關報告，用語必須符合台灣官方標準。**

分析要求：
1. 根據 M1-M5 的成熟度分數（0-10分），識別最弱的領域
2. 提供具體、可執行的改善建議
3. 優先關注最關鍵的問題

輸出格式：
提供 1-2 句精簡、可執行的建議（使用台灣繁體中文正式用語）。";

            var userPrompt = $@"風險評估分數：
• M1 交接：{m1} 分
• M2 追溯：{m2} 分
• M3 變更：{m3} 分
• M4 驗收：{m4} 分
• M5 溝通：{m5} 分

請提供風險改善建議。";

            var requestBody = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.8,
                max_tokens = 200
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("chat/completions", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(responseContent);
            
            var advice = jsonDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            // 存儲中文版本到 Session
            if (!string.IsNullOrEmpty(advice))
            {
                HttpContext.Session.SetString("PersonalizedAdvice_zh-TW", advice);
                _logger.LogInformation("已將中文版本的風險改善建議存儲到 Session");
                
                // 如果是英文請求，翻譯中文版本
                if (isEnglish)
                {
                    var translatedAdvice = await TranslateAdviceToEnglish(advice);
                    return Json(new { advice = translatedAdvice });
                }
            }
            
            return Json(new { advice });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "風險改善建議生成失敗");
            var lang = data?.GetValueOrDefault("lang") ?? "zh-TW";
            var errorMsg = lang == "en-US" 
                ? $"Failed to generate risk improvement recommendations: {ex.Message}"
                : $"風險改善建議生成失敗：{ex.Message}";
            return Json(new { advice = errorMsg });
        }
    }

    // 翻譯中文建議為英文（保持格式和結構一致）
    private async Task<string> TranslateAdviceToEnglish(string chineseAdvice)
    {
        try
        {
            var translationPrompt = @"You are a professional translator specializing in technical and business documents. Translate the following Traditional Chinese text to English while maintaining:

1. The exact same meaning and tone
2. Professional business English appropriate for government and enterprise contexts
3. Keep it concise (1-2 sentences as the original)

Translate the following text:";

            var requestBody = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "system", content = translationPrompt },
                    new { role = "user", content = chineseAdvice }
                },
                temperature = 0.3,  // 較低溫度，確保翻譯一致性
                max_tokens = 300
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("chat/completions", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(responseContent);
            
            var translatedText = jsonDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            
            _logger.LogInformation("成功將中文風險改善建議翻譯為英文");
            return translatedText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "翻譯失敗，返回原始中文版本");
            // 如果翻譯失敗，返回中文版本（前端可以處理）
            return chineseAdvice;
        }
    }

    // M6-M8 AI摘要API
    [HttpPost]
    public async Task<IActionResult> GenerateInsights([FromBody] Dictionary<string, string> m678)
    {
        try
        {
            var lang = m678?.GetValueOrDefault("lang") ?? "zh-TW";
            // 從請求體中提取 M1-M5 分數和 M6-M8 開放式問題
            var m1 = m678?.GetValueOrDefault("m1") ?? m678?.GetValueOrDefault("M1") ?? "0";
            var m2 = m678?.GetValueOrDefault("m2") ?? m678?.GetValueOrDefault("M2") ?? "0";
            var m3 = m678?.GetValueOrDefault("m3") ?? m678?.GetValueOrDefault("M3") ?? "0";
            var m4 = m678?.GetValueOrDefault("m4") ?? m678?.GetValueOrDefault("M4") ?? "0";
            var m5 = m678?.GetValueOrDefault("m5") ?? m678?.GetValueOrDefault("M5") ?? "0";
            var m6 = m678?.GetValueOrDefault("m6") ?? m678?.GetValueOrDefault("M6") ?? "";
            var m7 = m678?.GetValueOrDefault("m7") ?? m678?.GetValueOrDefault("M7") ?? "";
            var m8 = m678?.GetValueOrDefault("m8") ?? m678?.GetValueOrDefault("M8") ?? "";

            var isEnglish = lang == "en-US";

            // 如果沒有開放式問題數據，返回提示
            if (string.IsNullOrWhiteSpace(m6) && string.IsNullOrWhiteSpace(m7) && string.IsNullOrWhiteSpace(m8))
            {
                var noDataMsg = isEnglish 
                    ? "No open-ended question data provided"
                    : "未提供開放式問題數據";
                return Json(new { insights = noDataMsg });
            }

            // 如果請求英文版本，先檢查 Session 中是否有中文版本
            if (isEnglish)
            {
                var chineseInsights = HttpContext.Session.GetString("AIInsights_zh-TW");
                if (!string.IsNullOrEmpty(chineseInsights))
                {
                    // 有中文版本，進行翻譯
                    _logger.LogInformation("發現中文版本的 AI 洞察，進行翻譯");
                    var translatedInsights = await TranslateToEnglish(chineseInsights);
                    return Json(new { insights = translatedInsights });
                }
            }
            
            // 生成中文版本（優先）或英文版本（如果沒有中文版本且請求英文）
            // 如果是中文請求，生成並存儲；如果是英文請求但沒有中文版本，也生成（但通常不會發生）

            // 構建 OpenAI API 請求 - 改進的 prompt（支援多語言）
            var systemPrompt = isEnglish
                ? @"You are a senior project management and documentation risk assessment expert. Deeply analyze the user's risk assessment results, combining quantitative scores and qualitative descriptions to identify root causes and provide specific, actionable recommendations.

Important terminology clarification:
- PG = Programmer (程式設計師/程式開發人員), NOT Project Manager
- SA = System Analyst (系統分析師)
- PM = Project Manager (專案經理)
- PL = Project Leader (專案負責人)
- SD = System Designer (系統設計師)
- SE = System Engineer (系統工程師)
- BA = Business Analyst (業務分析師)
- QA = Quality Assurance (品質保證)
- DBA = Database Administrator (資料庫管理員)
- DevOps = Development Operations (開發運維工程師)
- SRE = Site Reliability Engineering (站點可靠性工程師)

Analysis requirements:
1. Combine M1-M5 maturity scores (0-10) and M6-M8 text descriptions for in-depth analysis
2. Identify root causes, not surface symptoms
3. Provide 3-5 prioritized specific improvement recommendations, each containing multiple actionable steps
4. Each recommendation must be actionable with bulleted specific action items
5. Total word count should be approximately 600-800 words, ensuring detailed and complete content
6. Use bulleted list format to make recommendations clear and easy to read
7. Ensure complete output without truncation - use full sentences and finish all thoughts

Output format (in English):
【Core Issue】
Summarize the most critical problem in 2-3 sentences, explaining the root cause.

【Recommendation 1】Title of highest priority recommendation
• Specific action item one
• Specific action item two
• Specific action item three
(Add more action items as needed)

【Recommendation 2】Title of second priority recommendation
• Specific action item one
• Specific action item two
• Specific action item three

【Recommendation 3】Other important improvement directions (if needed)
• Specific action item one
• Specific action item two

【Summary】
Summarize the expected effects after implementing these recommendations in 1-2 sentences.

IMPORTANT: Complete your response fully within the specified length. Use bulleted lists for all action items. Do not cut off mid-sentence."
                : @"你是資深的專案管理和文件風險評估專家，專門服務台灣企業。請深入分析用戶的風險評估結果，結合量化分數和質性描述，識別問題的根本原因，並提供具體、可執行的建議。

**極重要：請嚴格使用台灣繁體中文正式用語，絕對禁止使用大陸簡體中文用語。這是政府機關報告，用語必須符合台灣官方標準。**

**必須使用的台灣用語（絕對禁止使用對應的大陸用語）：**
- 使用「執行」、「實施」、「落實」絕對禁止「推進」、「推動」、「開展」
- 使用「品質」、「資訊」、「軟體」絕對禁止「質量」、「信息」、「软件」
- 使用「專案」、「專責」絕對禁止「項目」、「专职」
- 使用「人力」、「人員」、「人力資源」絕對禁止「人手」、「人力资源」
- 使用「確保」、「加強」、「強化」絕對禁止「确保」、「加强」、「强化」（注意：台灣用「強化」，但不可用「强化」）
- 使用「建立」、「建置」、「設置」絕對禁止「建設」、「搭建」、「设置」
- 使用「改善」、「優化」、「改進」絕對禁止「改进」、「优化」、「改善」（注意：台灣用「改善」，但不可用「改进」）
- 使用「進行」絕對禁止「開展」
- 使用「團隊」、「組織」絕對禁止「團體」、「组织」
- 使用「流程」、「程序」絕對禁止「過程」、「流程」（注意：台灣用「流程」，但不可用「过程」）
- 使用「資料」、「資料庫」絕對禁止「数据」、「数据库」
- 使用「系統」絕對禁止「系统」
- 使用「網路」絕對禁止「网络」
- 使用「檔案」絕對禁止「文件」、「文件」（注意：台灣用「檔案」，大陸用「文件」）
- 使用「規劃」絕對禁止「规划」
- 使用「評估」絕對禁止「评估」
- 使用「管理」絕對禁止「管理」（注意：兩岸用字相同，但語境不同）
- 使用「營運」絕對禁止「运营」
- 使用「設計」絕對禁止「设计」
- 使用「開發」絕對禁止「开发」
- 使用「維護」絕對禁止「维护」
- 使用「整合」絕對禁止「整合」（注意：兩岸用字相同）
- 使用「檢核」、「查核」、「查驗」絕對禁止「检查」、「核查」
- 使用「驗收」絕對禁止「验收」
- 使用「追溯」、「追蹤」絕對禁止「追溯」、「跟踪」
- 使用「交接」絕對禁止「交接」（注意：兩岸用字相同）
- 使用「溝通」、「協作」絕對禁止「沟通」、「协作」
- 使用「落實」、「實作」絕對禁止「落实」、「实施」（注意：台灣多用「落實」，大陸用「实施」）

**語法習慣：**
- 使用「進行」、「執行」而非「開展」
- 使用「應」、「應該」而非「应」、「应该」
- 使用「將」、「將會」而非「将」、「将会」
- 使用「於」、「在」而非「于」、「在」（注意：台灣多用「在」，大陸用「于」）
- 使用「與」、「及」而非「与」、「及」（注意：台灣多用「與」，大陸用「与」）

**絕對禁止的大陸用語範例：**「推进」、「推动」、「开展」、「质量」、「信息」、「软件」、「项目」、「人手」、「确保」、「加强」、「强化」、「建设」、「搭建」、「设置」、「改进」、「优化」、「团体」、「组织」、「过程」、「流程」、「数据」、「数据库」、「系统」、「网络」、「文件」、「规划」、「评估」、「运营」、「设计」、「开发」、「维护」、「检查」、「核查」、「验收」、「跟踪」、「沟通」、「协作」、「落实」、「实施」等所有簡體字或大陸用語。

重要術語說明：
- PG = Programmer（程式設計師/程式開發人員），不是專案管理人員（Project Manager）
- SA = System Analyst（系統分析師）
- PM = Project Manager（專案經理）
- PL = Project Leader（專案負責人）
- SD = System Designer（系統設計師）
- SE = System Engineer（系統工程師）
- BA = Business Analyst（業務分析師）
- QA = Quality Assurance（品質保證）
- DBA = Database Administrator（資料庫管理員）
- DevOps = Development Operations（開發運維工程師）
- SRE = Site Reliability Engineering（站點可靠性工程師）

分析要求：
1. 結合 M1-M5 的成熟度分數（0-10分）和 M6-M8 的文字描述進行深度分析
2. 識別問題的根本原因，而非表面症狀
3. 提供 3-5 個優先級排序的具體改善建議，每個建議下包含多個可執行的行動項目
4. 建議需具備可執行性，每個建議都應包含條列式的具體行動步驟
5. 總字數控制在約 600-800 字左右，確保內容詳實且完整
6. 使用條列式表達，讓建議更加清晰易讀
7. 確保完整輸出，不要中途截斷，使用完整句子並完成所有想法

輸出格式（使用台灣繁體中文）：
【核心問題】
用 2-3 句話總結最關鍵的問題，說明根本原因。

【改善建議1】優先級最高的建議標題
• 具體行動項目一
• 具體行動項目二
• 具體行動項目三
（可視需要增加更多行動項目）

【改善建議2】次優先的建議標題
• 具體行動項目一
• 具體行動項目二
• 具體行動項目三

【改善建議3】其他重要改善方向（如有需要）
• 具體行動項目一
• 具體行動項目二

【總結】
用 1-2 句話總結實施這些建議後的預期效果。

重要：請使用台灣繁體中文正式用語，完整輸出您的回應，不要在句子中間截斷。";

            var indicatorLabels = isEnglish
                ? new { M1 = "M1 Handover", M2 = "M2 Traceability", M3 = "M3 Change", M4 = "M4 Acceptance", M5 = "M5 Communication", Q6 = "Question 6 - Main Challenges", Q7 = "Question 7 - Areas to Improve", Q8 = "Question 8 - Other Information" }
                : new { M1 = "M1 交接", M2 = "M2 追溯", M3 = "M3 變更", M4 = "M4 驗收", M5 = "M5 溝通", Q6 = "問題6 - 主要挑戰", Q7 = "問題7 - 期望改善", Q8 = "問題8 - 其他資訊" };

            var userPrompt = isEnglish
                ? $@"Risk Assessment Results:

Quantitative Indicators (Maturity Score 0-10):
• {indicatorLabels.M1}: {m1} points
• {indicatorLabels.M2}: {m2} points
• {indicatorLabels.M3}: {m3} points
• {indicatorLabels.M4}: {m4} points
• {indicatorLabels.M5}: {m5} points

Qualitative Descriptions (Open-ended Questions):
• {indicatorLabels.Q6}: {m6}
• {indicatorLabels.Q7}: {m7}
• {indicatorLabels.Q8}: {m8}

Please conduct a deep analysis and provide improvement recommendations."
                : $@"風險評估結果：

量化指標（成熟度分數 0-10）：
• {indicatorLabels.M1}：{m1} 分
• {indicatorLabels.M2}：{m2} 分
• {indicatorLabels.M3}：{m3} 分
• {indicatorLabels.M4}：{m4} 分
• {indicatorLabels.M5}：{m5} 分

質性描述（開放式問題）：
• {indicatorLabels.Q6}：{m6}
• {indicatorLabels.Q7}：{m7}
• {indicatorLabels.Q8}：{m8}

請進行深度分析並提供改善建議。";

            var requestBody = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.8,
                max_tokens = 1200  // 設定為 1200，支援更長篇且詳實的條列式內容（AI 會控制在 600-800 字左右）
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogInformation("發送 OpenAI API 請求，語言: {Lang}", lang);
            var response = await _httpClient.PostAsync("chat/completions", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("OpenAI API 錯誤: {StatusCode}, {Error}", response.StatusCode, errorContent);
                throw new HttpRequestException($"OpenAI API 錯誤: {response.StatusCode} - {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("收到 OpenAI API 回應，長度: {Length}", responseContent.Length);
            
            var jsonDoc = JsonDocument.Parse(responseContent);
            
            var insights = jsonDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            // 如果是中文版本，存儲在 Session 中供後續翻譯使用
            if (!isEnglish && !string.IsNullOrEmpty(insights))
            {
                HttpContext.Session.SetString("AIInsights_zh-TW", insights);
                _logger.LogInformation("已將中文版本的 AI 洞察存儲到 Session");
            }
            
            _logger.LogInformation("AI 洞察生成成功，長度: {Length}", insights?.Length ?? 0);
            return Json(new { insights });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI 洞察生成失敗");
            var lang = m678?.GetValueOrDefault("lang") ?? "zh-TW";
            var isEnglish = lang == "en-US";
            var errorMsg = isEnglish
                ? $"AI analysis failed: {ex.Message}"
                : $"AI 分析失敗：{ex.Message}";
            return Json(new { insights = errorMsg });
        }
    }

    // 翻譯中文洞察為英文（保持格式和結構一致）
    private async Task<string> TranslateToEnglish(string chineseInsights)
    {
        try
        {
            var translationPrompt = @"You are a professional translator specializing in technical and business documents. Translate the following Traditional Chinese text to English while maintaining:

1. The exact same structure and formatting (including section headings with 【】, bullet points, etc.)
2. The same number of recommendations and action items
3. Professional business English appropriate for government and enterprise contexts
4. All technical terminology (PG, SA, PM, PL, etc.) correctly translated

Translate the following text:";

            var requestBody = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "system", content = translationPrompt },
                    new { role = "user", content = chineseInsights }
                },
                temperature = 0.3,  // 較低溫度，確保翻譯一致性
                max_tokens = 1500
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("chat/completions", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(responseContent);
            
            var translatedText = jsonDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            
            _logger.LogInformation("成功將中文洞察翻譯為英文");
            return translatedText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "翻譯失敗，返回原始中文版本");
            // 如果翻譯失敗，返回中文版本（前端可以處理）
            return chineseInsights;
        }
    }

    // Report頁使用
    public IActionResult Report()
    {
        // 前端通過 localStorage 和 AJAX 獲取數據，這裡不需要讀取表單
        // 如果需要從後端傳遞數據，可以在 ViewBag 中設置
        ViewBag.AIEInsights = "【建議】等待AI分析...";
        return View();
    }    
}
