using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace UniCP.Services
{
    public class ZabbixService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly string _apiUrl;
        private readonly string _username;
        private readonly string _password;
        private string? _authToken;
        private bool _isAuthenticated = false;
        private readonly SemaphoreSlim _authLock = new(1, 1);

        public ZabbixService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            
            // Read from configuration
            _apiUrl = _configuration["Zabbix:ApiUrl"] ?? throw new InvalidOperationException("Zabbix:ApiUrl not configured");
            _username = _configuration["Zabbix:Username"] ?? throw new InvalidOperationException("Zabbix:Username not configured");
            _password = _configuration["Zabbix:Password"] ?? throw new InvalidOperationException("Zabbix:Password not configured");
        }

        public async Task EnsureAuthenticatedAsync()
        {
            if (_isAuthenticated && !string.IsNullOrEmpty(_authToken)) return;

            await _authLock.WaitAsync();
            try
            {
                if (!_isAuthenticated || string.IsNullOrEmpty(_authToken))
                {
                    await LoginAsync(_username, _password);
                    _isAuthenticated = true;
                }
            }
            finally
            {
                _authLock.Release();
            }
        }

        public async Task<string?> LoginAsync(string username, string password)
        {
            var request = new
            {
                jsonrpc = "2.0",
                method = "user.login",
                @params = new { username = username, password = password },
                id = 1
            };

            // Login returns a string token directly in 'result'
            _authToken = await SendRequestAsync<string>(request);
            return _authToken;
        }

        public async Task<List<ZabbixItem>> GetItemsAsync(string hostId, string keySearch)
        {
            await EnsureAuthenticatedAsync();
            
            object paramsPayload;
            if (!string.IsNullOrEmpty(keySearch))
            {
                paramsPayload = new
                {
                    output = new[] { "itemid", "name", "key_", "lastvalue", "units", "value_type" },
                    hostids = hostId,
                    search = new { key_ = keySearch },
                    searchWildcardsEnabled = true,
                    sortfield = "name"
                };
            }
            else
            {
                paramsPayload = new
                {
                    output = new[] { "itemid", "name", "key_", "lastvalue", "units", "value_type" },
                    hostids = hostId,
                    sortfield = "name"
                };
            }

            var request = new
            {
                jsonrpc = "2.0",
                method = "item.get",
                @params = paramsPayload,
                auth = _authToken,
                id = 2
            };

            var result = await SendRequestAsync<List<ZabbixItem>>(request);
            return result ?? new List<ZabbixItem>();
        }

        public async Task<double?> GetLatestValueAsync(string hostId, string keySearch)
        {
             var items = await GetItemsAsync(hostId, keySearch);
             var item = items.FirstOrDefault();
             if (item != null && double.TryParse(item.LastValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
             {
                 return val;
             }
             return null;
        }

        public async Task<List<ZabbixHistory>> GetHistoryAsync(string itemId, int historyType, long timeFrom, long timeTo)
        {
            await EnsureAuthenticatedAsync();
            
            // historyType: 0 - numeric float, 3 - numeric unsigned
            var request = new
            {
                jsonrpc = "2.0",
                method = "history.get",
                @params = new
                {
                    output = "extend",
                    history = historyType, 
                    itemids = itemId,
                    sortfield = "clock",
                    sortorder = "ASC",
                    time_from = timeFrom,
                    time_till = timeTo
                },
                auth = _authToken,
                id = 3
            };

            var result = await SendRequestAsync<List<ZabbixHistory>>(request);
            return result ?? new List<ZabbixHistory>();
        }

        private async Task<T?> SendRequestAsync<T>(object payload)
        {
            var json = JsonSerializer.Serialize(payload);
            
            // SECURITY: Mask password in logs
            var logJson = json;
            if (json.Contains("password"))
            {
                // Simple regex-like replacement for logging only
                int pIdx = json.IndexOf("password");
                if (pIdx > 0)
                {
                    int endIdx = json.IndexOf("}", pIdx);
                    if (endIdx > 0)
                        logJson = json.Substring(0, pIdx) + "password\":\"***MASKED***\"" + json.Substring(endIdx);
                }
            }

            Console.WriteLine($"[Zabbix Debug] Sending Request to: {_apiUrl}");
            Console.WriteLine($"[Zabbix Debug] Payload: {logJson}");

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_apiUrl, content);
            response.EnsureSuccessStatusCode();

            var responseData = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            
            // Deserialize into a wrapper that can hold either Result or Error
            var rpcResponse = JsonSerializer.Deserialize<JsonRpcResponse<T>>(responseData, options);

            if (rpcResponse == null)
                throw new Exception("Empty response from Zabbix API");

            if (rpcResponse.Error != null)
            {
                throw new Exception($"Zabbix API Error {rpcResponse.Error.Code}: {rpcResponse.Error.Message} | Data: {rpcResponse.Error.Data}");
            }

            return rpcResponse.Result;
        }
    }

    // Generic Wrapper
    public class JsonRpcResponse<T>
    {
        public string? Jsonrpc { get; set; }
        
        // This maps to "result" in JSON. 
        // If T is List<ZabbixItem>, this holds the list.
        public T? Result { get; set; }
        
        public JsonRpcError? Error { get; set; }
        public int Id { get; set; }
    }

    public class JsonRpcError
    {
        public int Code { get; set; }
        public string Message { get; set; } = "";
        public string Data { get; set; } = "";
    }

    // Models (Moved here, cleaned up wrappers)
    public class ZabbixItem 
    { 
        [JsonPropertyName("itemid")]
        public string ItemId { get; set; } = "";
        public string Name { get; set; } = "";
        [JsonPropertyName("key_")]
        public string Key { get; set; } = "";
        [JsonPropertyName("lastvalue")]
        public string LastValue { get; set; } = "";
        public string Units { get; set; } = "";
        [JsonPropertyName("value_type")]
        public string ValueType { get; set; } = "";
    }

    public class ZabbixHistory
    {
        public string Clock { get; set; } = "";
        public string Value { get; set; } = "";
        public double GetValue() => double.TryParse(Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : 0;
        public long GetTime() => long.TryParse(Clock, out long t) ? t : 0;
        public DateTime GetDateTime() => DateTimeOffset.FromUnixTimeSeconds(GetTime()).DateTime.ToLocalTime();
    }
}
