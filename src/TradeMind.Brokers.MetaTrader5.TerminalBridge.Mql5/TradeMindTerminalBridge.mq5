#property strict
#property version   "2.0"

input string BridgeUrl = "https://127.0.0.1:5001";
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
      "{\"protocolVersion\":\"1.0\",\"terminalVersion\":\"MT5\",\"terminalBuild\":%d,\"terminalArchitecture\":\"x64\",\"demoAccount\":%s,\"accountEnvironment\":\"%s\",\"tradingEnabled\":%s,\"readOnly\":false,\"accountId\":\"%I64d\",\"capabilities\":[\"account.read\",\"instrument.read\",\"orders.read\",\"positions.read\",\"heartbeat\",\"demo.market.write\",\"demo.position.close\"]}",
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
      string item = AccountJson();
      SendResult(commandId, true, "ACCOUNT", "Account returned", StringFormat("{\"items\":\"%s\"}", JsonEscape(StringFormat("[%s]", item))));
      return;
     }
   if(command == "get-instrument")
     {
      string symbol = JsonString(request, "instrument");
      if(StringLen(symbol) == 0) symbol = _Symbol;
      string item;
      if(!InstrumentJson(symbol, item))
        {
         SendResult(commandId, false, "SYMBOL_NOT_FOUND", "The instrument was not found", "{}");
         return;
        }
      SendResult(commandId, true, "INSTRUMENT", "Instrument returned", StringFormat("{\"instrument\":\"%s\"}", JsonEscape(item)));
      return;
     }
   if(command == "get-orders")
     {
      SendResult(commandId, true, "ORDERS", "Orders returned", StringFormat("{\"items\":\"%s\"}", JsonEscape(OrdersJson())));
      return;
     }
   if(command == "get-positions")
     {
      SendResult(commandId, true, "POSITIONS", "Positions returned", StringFormat("{\"items\":\"%s\"}", JsonEscape(PositionsJson())));
      return;
     }
   if(command == "submit-order")
     {
      SubmitDemoMarketOrder(commandId, request);
      return;
     }
   if(command == "close-position")
     {
      CloseDemoPosition(commandId, request);
      return;
     }
   SendResult(commandId, false, "LIVE_MODE_FORBIDDEN", "The terminal command is not supported", "{}");
  }

void SubmitDemoMarketOrder(string commandId, string request)
  {
   if(!IsDemo())
     {
      SendResult(commandId, false, "LIVE_MODE_FORBIDDEN", "Live execution is disabled", "{}");
      return;
     }
   if(!AccountInfoInteger(ACCOUNT_TRADE_ALLOWED))
     {
      SendResult(commandId, false, "DEMO_EXECUTION_REQUIRED", "Demo trading is not enabled", "{}");
      return;
     }
   if(OrdersTotal() > 0 || PositionsTotal() > 0)
     {
      SendResult(commandId, false, "POSITION_LIMIT", "The demo smoke test permits one order and one position only", "{}");
      return;
     }

   string symbol = JsonString(request, "instrument");
   string side = JsonString(request, "side");
   string orderType = JsonString(request, "order_type");
   double volume = StringToDouble(JsonString(request, "quantity"));
   if(StringLen(symbol) == 0 || (side != "Buy" && side != "Sell") || orderType != "Market")
     {
      SendResult(commandId, false, "INVALID_REQUEST", "Only a Demo market order with a valid instrument is supported", "{}");
      return;
     }
   if(!SymbolSelect(symbol, true) || !IsMinimumVolume(symbol, volume))
     {
      SendResult(commandId, false, "INVALID_VOLUME", "The requested volume is not the symbol minimum", "{}");
      return;
     }

   MqlTick tick;
   if(!SymbolInfoTick(symbol, tick))
     {
      SendResult(commandId, false, "NO_CONNECTION", "A current market tick is required", "{}");
      return;
     }
   MqlTradeRequest trade = {};
   MqlTradeResult result = {};
   trade.action = TRADE_ACTION_DEAL;
   trade.symbol = symbol;
   trade.volume = volume;
   trade.type = side == "Buy" ? ORDER_TYPE_BUY : ORDER_TYPE_SELL;
   trade.price = side == "Buy" ? tick.ask : tick.bid;
   trade.deviation = 20;
   trade.type_filling = ORDER_FILLING_IOC;
   if(!OrderSend(trade, result) || !TradeRetcodeSucceeded(result.retcode))
     {
      SendResult(commandId, false, NormalizeTradeError(result.retcode), "The demo order was rejected", "{}");
      return;
     }

   ulong positionTicket = FindPositionTicket(symbol);
   ulong orderTicket = result.order > 0 ? result.order : result.deal;
   ulong dealTicket = result.deal > 0 ? result.deal : orderTicket;
   string clientOrderId = JsonString(request, "client_order_id");
   string order = OrderPayload(orderTicket, AccountInfoInteger(ACCOUNT_LOGIN), clientOrderId, symbol, side, volume, result.price, "Filled");
   string execution = ExecutionPayload(dealTicket, orderTicket, AccountInfoInteger(ACCOUNT_LOGIN), symbol, volume, result.price, positionTicket);
   SendResult(commandId, true, "ORDER_SUBMITTED", "The demo order was accepted", StringFormat("{\"order\":\"%s\",\"execution\":\"%s\"}", JsonEscape(order), JsonEscape(execution)));
  }

void CloseDemoPosition(string commandId, string request)
  {
   if(!IsDemo())
     {
      SendResult(commandId, false, "LIVE_MODE_FORBIDDEN", "Live execution is disabled", "{}");
      return;
     }
   if(!AccountInfoInteger(ACCOUNT_TRADE_ALLOWED))
     {
      SendResult(commandId, false, "DEMO_EXECUTION_REQUIRED", "Demo trading is not enabled", "{}");
      return;
     }
   ulong positionTicket = (ulong)StringToInteger(JsonString(request, "position_id"));
   if(positionTicket == 0 || !PositionSelectByTicket(positionTicket))
     {
      SendResult(commandId, false, "POSITION_NOT_FOUND", "The demo position was not found", "{}");
      return;
     }
   string symbol = PositionGetString(POSITION_SYMBOL);
   double volume = PositionGetDouble(POSITION_VOLUME);
   ENUM_POSITION_TYPE positionType = (ENUM_POSITION_TYPE)PositionGetInteger(POSITION_TYPE);
   MqlTick tick;
   if(!SymbolInfoTick(symbol, tick))
     {
      SendResult(commandId, false, "NO_CONNECTION", "A current market tick is required for cleanup", "{}");
      return;
     }
   MqlTradeRequest trade = {};
   MqlTradeResult result = {};
   trade.action = TRADE_ACTION_DEAL;
   trade.position = positionTicket;
   trade.symbol = symbol;
   trade.volume = volume;
   trade.type = positionType == POSITION_TYPE_BUY ? ORDER_TYPE_SELL : ORDER_TYPE_BUY;
   trade.price = positionType == POSITION_TYPE_BUY ? tick.bid : tick.ask;
   trade.deviation = 20;
   trade.type_filling = ORDER_FILLING_IOC;
   if(!OrderSend(trade, result) || !TradeRetcodeSucceeded(result.retcode))
     {
      SendResult(commandId, false, "CLEANUP_FAILED", "The demo cleanup order was rejected", "{}");
      return;
     }
   ulong orderTicket = result.order > 0 ? result.order : result.deal;
   ulong dealTicket = result.deal > 0 ? result.deal : orderTicket;
   string side = positionType == POSITION_TYPE_BUY ? "Buy" : "Sell";
   string position = PositionPayload(positionTicket, AccountInfoInteger(ACCOUNT_LOGIN), symbol, side, volume, PositionGetDouble(POSITION_PRICE_OPEN), "Closed");
   string execution = ExecutionPayload(dealTicket, orderTicket, AccountInfoInteger(ACCOUNT_LOGIN), symbol, volume, result.price, positionTicket);
   SendResult(commandId, true, "POSITION_CLOSED", "The demo position was closed", StringFormat("{\"position\":\"%s\",\"execution\":\"%s\"}", JsonEscape(position), JsonEscape(execution)));
  }

void SendResult(string commandId, bool success, string code, string message, string fields)
  {
   string response;
   string payload = StringFormat("{\"commandId\":\"%s\",\"success\":%s,\"code\":\"%s\",\"message\":\"%s\",\"fields\":%s}", JsonEscape(commandId), success ? "true" : "false", JsonEscape(code), JsonEscape(message), fields);
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

string AccountJson()
  {
   double margin = AccountInfoDouble(ACCOUNT_MARGIN);
   double marginLevel = margin > 0.0 ? AccountInfoDouble(ACCOUNT_MARGIN_LEVEL) : 0.0;
   return StringFormat("{\"accountId\":\"%I64d\",\"name\":\"MT5 Demo\",\"currency\":\"%s\",\"accountType\":\"Demo\",\"environment\":\"Test\",\"balance\":%s,\"equity\":%s,\"margin\":%s,\"freeMargin\":%s,\"marginLevel\":%s,\"leverage\":%d,\"tradingEnabled\":%s,\"readOnly\":false,\"status\":\"Active\",\"updatedAtUtc\":\"%s\"}",
      AccountInfoInteger(ACCOUNT_LOGIN), AccountInfoString(ACCOUNT_CURRENCY), JsonNumber(AccountInfoDouble(ACCOUNT_BALANCE)), JsonNumber(AccountInfoDouble(ACCOUNT_EQUITY)), JsonNumber(margin), JsonNumber(AccountInfoDouble(ACCOUNT_MARGIN_FREE)), JsonNumber(marginLevel), (int)AccountInfoInteger(ACCOUNT_LEVERAGE), AccountInfoInteger(ACCOUNT_TRADE_ALLOWED) ? "true" : "false", UtcNow());
  }

bool InstrumentJson(string symbol, string &result)
  {
   if(!SymbolSelect(symbol, true)) return(false);
   long tradeMode = SymbolInfoInteger(symbol, SYMBOL_TRADE_MODE);
   if(tradeMode == SYMBOL_TRADE_MODE_DISABLED) return(false);
   string base = SymbolInfoString(symbol, SYMBOL_CURRENCY_BASE);
   string quote = SymbolInfoString(symbol, SYMBOL_CURRENCY_PROFIT);
   int digits = (int)SymbolInfoInteger(symbol, SYMBOL_DIGITS);
   double point = SymbolInfoDouble(symbol, SYMBOL_POINT);
   double tickSize = SymbolInfoDouble(symbol, SYMBOL_TRADE_TICK_SIZE);
   double tickValue = SymbolInfoDouble(symbol, SYMBOL_TRADE_TICK_VALUE);
   double contractSize = SymbolInfoDouble(symbol, SYMBOL_TRADE_CONTRACT_SIZE);
   double minimum = SymbolInfoDouble(symbol, SYMBOL_VOLUME_MIN);
   double maximum = SymbolInfoDouble(symbol, SYMBOL_VOLUME_MAX);
   double step = SymbolInfoDouble(symbol, SYMBOL_VOLUME_STEP);
   long stops = SymbolInfoInteger(symbol, SYMBOL_TRADE_STOPS_LEVEL);
   result = StringFormat("{\"instrument\":\"%s\",\"brokerSymbol\":\"%s\",\"assetClass\":\"Forex\",\"baseCurrency\":\"%s\",\"quoteCurrency\":\"%s\",\"pricePrecision\":%d,\"quantityPrecision\":%d,\"tickSize\":%s,\"tickValue\":%s,\"contractSize\":%s,\"minimumQuantity\":%s,\"maximumQuantity\":%s,\"quantityStep\":%s,\"minimumStopDistance\":%s,\"marketStatus\":\"Open\",\"supportedOrderTypes\":[\"Market\"],\"updatedAtUtc\":\"%s\"}", symbol, symbol, base, quote, digits, VolumeDigits(step), JsonNumber(tickSize), JsonNumber(tickValue), JsonNumber(contractSize), JsonNumber(minimum), JsonNumber(maximum), JsonNumber(step), JsonNumber(stops * point), UtcNow());
   return(true);
  }

string OrdersJson()
  {
   string result = "[";
   for(int index = 0; index < OrdersTotal(); index++)
     {
      ulong ticket = OrderGetTicket(index);
      if(ticket == 0) continue;
      if(StringLen(result) > 1) result += ",";
      string symbol = OrderGetString(ORDER_SYMBOL);
      string side = OrderGetInteger(ORDER_TYPE) == ORDER_TYPE_BUY_LIMIT || OrderGetInteger(ORDER_TYPE) == ORDER_TYPE_BUY_STOP ? "Buy" : "Sell";
      result += OrderPayload(ticket, AccountInfoInteger(ACCOUNT_LOGIN), "", symbol, side, OrderGetDouble(ORDER_VOLUME_CURRENT), OrderGetDouble(ORDER_PRICE_OPEN), "Open");
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
      string symbol = PositionGetString(POSITION_SYMBOL);
      string side = PositionGetInteger(POSITION_TYPE) == POSITION_TYPE_BUY ? "Buy" : "Sell";
      result += PositionPayload(ticket, AccountInfoInteger(ACCOUNT_LOGIN), symbol, side, PositionGetDouble(POSITION_VOLUME), PositionGetDouble(POSITION_PRICE_OPEN), "Open");
     }
   return(result + "]");
  }

string OrderPayload(ulong orderTicket, long accountId, string clientOrderId, string symbol, string side, double volume, double price, string status)
  {
   string now = UtcNow();
   return StringFormat("{\"orderId\":\"%I64u\",\"executionId\":\"%I64u\",\"accountId\":\"%I64d\",\"clientOrderId\":\"%s\",\"instrument\":\"%s\",\"side\":\"%s\",\"orderType\":\"Market\",\"quantity\":%s,\"filledQuantity\":%s,\"requestedPrice\":null,\"averageFillPrice\":%s,\"status\":\"%s\",\"timeInForce\":\"Day\",\"createdAtUtc\":\"%s\",\"updatedAtUtc\":\"%s\"}", orderTicket, orderTicket, accountId, JsonEscape(clientOrderId), JsonEscape(symbol), side, JsonNumber(volume), JsonNumber(volume), JsonNumber(price), status, now, now);
  }

string PositionPayload(ulong positionTicket, long accountId, string symbol, string side, double volume, double price, string status)
  {
   string now = UtcNow();
   return StringFormat("{\"positionId\":\"%I64u\",\"accountId\":\"%I64d\",\"instrument\":\"%s\",\"side\":\"%s\",\"quantity\":%s,\"averagePrice\":%s,\"openedAtUtc\":\"%s\",\"updatedAtUtc\":\"%s\",\"status\":\"%s\"}", positionTicket, accountId, JsonEscape(symbol), side, JsonNumber(volume), JsonNumber(price), now, now, status);
  }

string ExecutionPayload(ulong dealTicket, ulong orderTicket, long accountId, string symbol, double volume, double price, ulong positionTicket)
  {
   string positionId = positionTicket == 0 ? "" : StringFormat("%I64u", positionTicket);
   return StringFormat("{\"executionId\":\"%I64u\",\"orderId\":\"%I64u\",\"accountId\":\"%I64d\",\"status\":\"Filled\",\"filledQuantity\":%s,\"averagePrice\":%s,\"occurredAtUtc\":\"%s\",\"positionId\":%s}", dealTicket, orderTicket, accountId, JsonNumber(volume), JsonNumber(price), UtcNow(), StringLen(positionId) == 0 ? "null" : "\"" + positionId + "\"");
  }

ulong FindPositionTicket(string symbol)
  {
   if(!PositionSelect(symbol)) return(0);
   return((ulong)PositionGetInteger(POSITION_TICKET));
  }

bool IsMinimumVolume(string symbol, double volume)
  {
   double minimum = SymbolInfoDouble(symbol, SYMBOL_VOLUME_MIN);
   double maximum = SymbolInfoDouble(symbol, SYMBOL_VOLUME_MAX);
   double step = SymbolInfoDouble(symbol, SYMBOL_VOLUME_STEP);
   if(volume < minimum || volume > maximum || step <= 0.0) return(false);
   if(MathAbs(volume - minimum) > step / 100.0) return(false);
   return(MathAbs((volume - minimum) / step - MathRound((volume - minimum) / step)) < 0.0000001);
  }

bool TradeRetcodeSucceeded(uint retcode)
  {
   return(retcode == TRADE_RETCODE_DONE || retcode == TRADE_RETCODE_DONE_PARTIAL || retcode == TRADE_RETCODE_PLACED);
  }

string NormalizeTradeError(uint retcode)
  {
   if(retcode == TRADE_RETCODE_INVALID_VOLUME) return("INVALID_VOLUME");
   if(retcode == TRADE_RETCODE_MARKET_CLOSED) return("MARKET_CLOSED");
   if(retcode == TRADE_RETCODE_NO_MONEY) return("INSUFFICIENT_MARGIN");
   if(retcode == TRADE_RETCODE_TRADE_DISABLED) return("DEMO_EXECUTION_REQUIRED");
   return("ORDER_REJECTED");
  }

string JsonString(string json, string name)
  {
   string marker = "\"" + name + "\":\"";
   int start = StringFind(json, marker);
   if(start < 0) return("");
   start += StringLen(marker);
   int end = start;
   while(end < StringLen(json))
     {
      if(StringGetCharacter(json, end) == '"' && (end == start || StringGetCharacter(json, end - 1) != '\\')) break;
      end++;
     }
   if(end >= StringLen(json)) return("");
   return(StringSubstr(json, start, end - start));
  }

string JsonEscape(string value)
  {
   StringReplace(value, "\\", "\\\\");
   StringReplace(value, "\"", "\\\"");
   StringReplace(value, "\r", "\\r");
   StringReplace(value, "\n", "\\n");
   return(value);
  }

string JsonNumber(double value)
  {
   return(DoubleToString(value, 8));
  }

int VolumeDigits(double step)
  {
   int digits = 0;
   while(digits < 8 && MathAbs(step - NormalizeDouble(step, digits)) > 0.000000001) digits++;
   return(digits);
  }

string UtcNow()
  {
   MqlDateTime value;
   TimeToStruct(TimeGMT(), value);
   return(StringFormat("%04d-%02d-%02dT%02d:%02d:%02dZ", value.year, value.mon, value.day, value.hour, value.min, value.sec));
  }
