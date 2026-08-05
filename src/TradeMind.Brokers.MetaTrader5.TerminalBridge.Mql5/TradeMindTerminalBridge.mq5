#property strict
#property version   "1.0"

input string BridgeUrl = "https://localhost:5001";
input string AgentToken = "";
input int PollIntervalSeconds = 1;

string LastCommandId = "";

int OnInit()
  {
   if(StringLen(BridgeUrl) == 0 || StringLen(AgentToken) == 0)
      return(INIT_PARAMETERS_INCORRECT);
   EventSetTimer(MathMax(1, PollIntervalSeconds));
   return(INIT_SUCCEEDED);
  }

void OnDeinit(const int reason)
  {
   EventKillTimer();
  }

void OnTimer()
  {
   PollBridge();
  }

void PollBridge()
  {
   string response;
   string payload = StringFormat(
      "{\"protocolVersion\":\"1.0\",\"terminalVersion\":\"MT5\",\"terminalBuild\":%d,\"terminalArchitecture\":\"x64\",\"demoAccount\":%s,\"accountEnvironment\":\"%s\",\"tradingEnabled\":%s,\"readOnly\":false,\"accountId\":\"%I64d\",\"capabilities\":[\"account.read\",\"instrument.read\",\"orders.read\",\"positions.read\",\"heartbeat\"]}",
      (int)TerminalInfoInteger(TERMINAL_BUILD),
      IsDemo() ? "true" : "false",
      AccountEnvironment(),
      AccountInfoInteger(ACCOUNT_TRADE_ALLOWED) ? "true" : "false",
      AccountInfoInteger(ACCOUNT_LOGIN));

   if(!Post("/terminal/v1/agent/poll", payload, response))
      return;

   string commandId = JsonString(response, "id");
   string command = JsonString(response, "command");
   if(StringLen(commandId) == 0 || StringLen(command) == 0)
      return;

   LastCommandId = commandId;
   ExecuteCommand(commandId, command, response);
  }

void ExecuteCommand(string commandId, string command, string request)
  {
   if(command == "heartbeat")
     {
      SendResult(commandId, true, "HEARTBEAT", "Heartbeat acknowledged", "{}");
      return;
     }
   if(command == "get-account" || command == "get-accounts")
     {
      string item = StringFormat("{\"accountId\":\"%I64d\",\"environment\":\"%s\",\"accountType\":\"Demo\",\"currency\":\"%s\",\"balance\":%.8f,\"equity\":%.8f}", AccountInfoInteger(ACCOUNT_LOGIN), AccountEnvironment(), AccountInfoString(ACCOUNT_CURRENCY), AccountInfoDouble(ACCOUNT_BALANCE), AccountInfoDouble(ACCOUNT_EQUITY));
      SendResult(commandId, true, "ACCOUNT", "Account returned", StringFormat("{\"items\":[%s]}", item));
      return;
     }
   if(command == "get-instrument")
     {
      string symbol = JsonString(request, "instrument");
      if(StringLen(symbol) == 0) symbol = _Symbol;
      if(!SymbolSelect(symbol, true))
        {
         SendResult(commandId, false, "SYMBOL_NOT_FOUND", "The instrument was not found", "{}");
         return;
        }
      int digits = (int)SymbolInfoInteger(symbol, SYMBOL_DIGITS);
      double point = SymbolInfoDouble(symbol, SYMBOL_POINT);
      string item = StringFormat("{\"instrument\":\"%s\",\"assetClass\":\"%s\",\"marketStatus\":\"%s\",\"priceScale\":%d,\"point\":%.8f}", symbol, "Unknown", "Open", digits, point);
      SendResult(commandId, true, "INSTRUMENT", "Instrument returned", StringFormat("{\"instrument\":%s}", item));
      return;
     }
   if(command == "get-orders")
     {
      SendResult(commandId, true, "ORDERS", "Orders returned", StringFormat("{\"items\":%s}", OrdersJson()));
      return;
     }
   if(command == "get-positions")
     {
      SendResult(commandId, true, "POSITIONS", "Positions returned", StringFormat("{\"items\":%s}", PositionsJson()));
      return;
     }
   SendResult(commandId, false, "UNKNOWN", "The terminal command is not supported", "{}");
  }

void SendResult(string commandId, bool success, string code, string message, string fields)
  {
   string response;
   string payload = StringFormat("{\"commandId\":\"%s\",\"success\":%s,\"code\":\"%s\",\"message\":\"%s\",\"fields\":%s}", commandId, success ? "true" : "false", code, message, fields);
   Post("/terminal/v1/agent/result", payload, response);
  }

bool Post(string route, string payload, string &response)
  {
   char data[];
   char result[];
   string resultHeaders;
   StringToCharArray(payload, data, 0, StringLen(payload), CP_UTF8);
   string headers = "Content-Type: application/json\r\nX-TradeMind-Agent-Token: " + AgentToken + "\r\n";
   int status = WebRequest("POST", BridgeUrl + route, headers, 10000, data, result, resultHeaders);
   if(status < 200 || status >= 300)
      return(false);
   response = CharArrayToString(result, 0, -1, CP_UTF8);
   return(true);
  }

bool IsDemo()
  {
   return((ENUM_ACCOUNT_TRADE_MODE)AccountInfoInteger(ACCOUNT_TRADE_MODE) == ACCOUNT_TRADE_MODE_DEMO);
  }

string AccountEnvironment()
  {
   return(IsDemo() ? "Demo" : "Live");
  }

string OrdersJson()
  {
   string result = "[";
   for(int index = 0; index < OrdersTotal(); index++)
     {
      ulong ticket = OrderGetTicket(index);
      if(ticket == 0) continue;
      if(StringLen(result) > 1) result += ",";
      result += StringFormat("{\"orderId\":\"%I64u\",\"instrument\":\"%s\",\"status\":\"Open\"}", ticket, OrderGetString(ORDER_SYMBOL));
     }
   return(result + "]");
  }

string PositionsJson()
  {
   string result = "[";
   for(int index = 0; index < PositionsTotal(); index++)
     {
      ulong ticket = PositionGetTicket(index);
      if(ticket == 0) continue;
      if(StringLen(result) > 1) result += ",";
      result += StringFormat("{\"positionId\":\"%I64u\",\"instrument\":\"%s\",\"status\":\"Open\"}", ticket, PositionGetString(POSITION_SYMBOL));
     }
   return(result + "]");
  }

string JsonString(string json, string key)
  {
   string marker = "\"" + key + "\":\"";
   int start = StringFind(json, marker);
   if(start < 0) return("");
   start += StringLen(marker);
   int end = StringFind(json, "\"", start);
   if(end < 0) return("");
   return(StringSubstr(json, start, end - start));
  }
