﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GalaSoft.MvvmLight.Ioc;
using System.Windows.Threading;
using GalaSoft.MvvmLight.Messaging;
using Asset_Management_Platform.Messages;
using System.Collections.ObjectModel;
using Asset_Management_Platform.SecurityClasses;

namespace Asset_Management_Platform.Utility
{
    public class PortfolioManagementService : IPortfolioManagementService
    {
        private IStockDataService _stockDataService;
        private IPortfolioDatabaseService _portfolioDatabaseService;
        private DispatcherTimer _timer;
        private List<Security> _securityDatabaseList;
        private List<LimitOrder> _limitOrderList;
        private List<Taxlot> _portfolioTaxlots;
        private List<Position> _portfolioPositions;
        private List<Security> _portfolioSecurities;

        public List<LimitOrder> LimitOrderList //used to display in MainViewModel
        {
            get
            {
                return _limitOrderList;
            }
        }
        
        public PortfolioManagementService(IStockDataService stockDataService, IPortfolioDatabaseService portfolioDatabaseService)
        {
            _stockDataService = stockDataService;
            _portfolioDatabaseService = portfolioDatabaseService;

            //Load known security info from SQL DB
            _securityDatabaseList = _stockDataService.LoadSecurityDatabase();

            //Download limit orders from SQL DB
            GetLimitOrderList();

            //Create the core List<T>'s of taxlots, positions, and securities
            BuildPortfolioSecurities();

            _timer = new DispatcherTimer();
            _timer.Tick += _timer_Tick;
            _timer.Interval = new TimeSpan(0, 0, 10);    
        }


        /// <summary>
        /// Creates the list of taxlots, positions, and securities owned.
        /// </summary>
        private void BuildPortfolioSecurities()
        {                 
            //Get taxlots from SQL DB                
            _portfolioTaxlots = _portfolioDatabaseService.GetTaxlotsFromDatabase();

            //Gather all tickers and get pricing data
            var tickers = new List<string>();
            foreach (var lot in _portfolioTaxlots)
            {
                if (!tickers.Contains(lot.Ticker))
                    tickers.Add(lot.Ticker);
            }

            //Get updated security data then append Yahoo API data for Mutual Funds 
            //with SQL DB's record of asset class & categories.
            //Ideally a future API will provide this data in previous steps
            var rawSecurities = _stockDataService.GetSecurityInfo(tickers);
            _portfolioSecurities = _stockDataService.GetMutualFundExtraData(rawSecurities);           

            //If taxlots exist, build positions with updated pricing data.
            if (_portfolioTaxlots.Count > 0)
                _portfolioPositions = _portfolioDatabaseService.GetPositionsFromTaxlots(_portfolioSecurities);
            else
                _portfolioPositions = new List<Position>();

            //Update all Positions' taxlot pricing
            foreach (var pos in _portfolioPositions)
            {
                var security = _portfolioSecurities.Find(s => s.Ticker == pos.Ticker);
                pos.UpdateTaxlotPrices(security.LastPrice);
            }
        }

        private void GetLimitOrderList()
        {
            _limitOrderList = _portfolioDatabaseService.LoadLimitOrdersFromDatabase();
        }

        /// <summary>
        /// Evaluate trade instructions and add to portfolio if appropriate.
        /// A limit order that is not active will be added to the list of 
        /// limit orders.
        /// </summary>
        /// <param name="trade"></param>
        public void Buy(Trade trade)
        {
            var limitType = false;

            //Check if any values are null or useless
            var validOrder = CheckOrderTerms(trade);
            var activeLimitOrder = CheckOrderLimit(trade);
            if (trade.Terms == "Limit" || trade.Terms == "Stop Limit" || trade.Terms == "Stop")
                limitType = true;

            if (validOrder && limitType && !activeLimitOrder)
            {
                //Order is valid but limit prevents execution
                CreateLimitOrder(trade);
                return;
            }

            if (validOrder && limitType && activeLimitOrder)
            {
                //Order is valid and a limit-type and is active
                AddPosition(trade);
                return;
            }

            if (validOrder && trade.Terms == "Market")
            {
                //Order is valid and a market order
                AddPosition(trade);
            }
        }

        private void AddPosition(Trade trade)
        {
            if (!_securityDatabaseList.Any(s => s.Ticker == trade.Ticker))
                _securityDatabaseList.Add(trade.Security);

            //Check to confirm that shares of this security aren't already owned
            if (trade.Security is Stock && !_portfolioPositions.Any(s => s.Ticker == trade.Ticker))
            {
                //Create taxlot and position, then add to position list
                var taxlot = new Taxlot(trade.Ticker, trade.Shares, trade.Security.LastPrice, DateTime.Now, trade.Security, trade.Security.LastPrice);
                var position = new Position(taxlot, trade.Security);
                _portfolioDatabaseService.AddToPortfolioDatabase(position);
            }
            //Ticker exists in portfolio and security is stock
            else if (trade.Security is Stock && _portfolioPositions.Any(s => s.Ticker == trade.Ticker))
            {
                //Create new taxlot and add to existing position
                var taxlot = new Taxlot(trade.Ticker, trade.Shares, trade.Security.LastPrice, DateTime.Now, trade.Security, trade.Security.LastPrice);
                _portfolioDatabaseService.AddToPortfolioDatabase(taxlot);
            }
            //Ticker is not already owned and is a MutualFund
            else if (trade.Security is MutualFund && !_portfolioPositions.Any(s => s.Ticker == trade.Ticker))
            {
                //Create new taxlot and add to existing position
                var taxlot = new Taxlot(trade.Ticker, trade.Shares, trade.Security.LastPrice, DateTime.Now, trade.Security, trade.Security.LastPrice);
                var position = new Position(taxlot, trade.Security);
                _portfolioDatabaseService.AddToPortfolioDatabase(position);
            }
            else if (trade.Security is MutualFund && _portfolioPositions.Any(s => s.Ticker == trade.Ticker))
            {
                //Create new taxlot and add to existing position
                var taxlot = new Taxlot(trade.Ticker, trade.Shares, trade.Security.LastPrice, DateTime.Now, trade.Security, trade.Security.LastPrice);
                _portfolioDatabaseService.AddToPortfolioDatabase(taxlot);
            }
        }

        private void CreateLimitOrder(Trade trade)
        {
            var newLimitOrder = new LimitOrder(trade);

            if (_limitOrderList == null)
                _limitOrderList = new List<LimitOrder>();

            _limitOrderList.Add(newLimitOrder);
        }

        private bool CheckOrderTerms(Trade trade)
        {
            var security = trade.Security;
            var ticker = trade.Ticker;
            var shares = trade.Shares;
            var terms = trade.Terms;
            var limit = trade.Limit;
            var orderDuration = trade.OrderDuration;

            if (trade.Terms == "Limit" || trade.Terms == "Stop Limit" || trade.Terms == "Stop" && limit <= 0)
                return false;

            if (security != null && !string.IsNullOrEmpty(ticker) && shares > 0 
                && !string.IsNullOrEmpty(terms) && !string.IsNullOrEmpty(orderDuration))
                return true;
            return false;
        }

        /// <summary>
        /// Directs the executing code to the proper method for disposing
        /// of the security type. No real differences at the moment, but 
        /// there may be later.
        /// </summary>
        /// <param name="security"></param>
        /// <param name="ticker"></param>
        /// <param name="shares"></param>
        public void Sell(Trade trade)
        {
            var limitType = false;

            //Check if any values are null or useless
            var validOrder = CheckOrderTerms(trade);
            var isActiveLimitOrder = CheckOrderLimit(trade);
            if (trade.Terms == "Limit" || trade.Terms == "Stop Limit" || trade.Terms == "Stop")
                limitType = true;

            if (validOrder && limitType && !isActiveLimitOrder)
            {
                //Order is valid but limit prevents execution
                CreateLimitOrder(trade);
                return;
            }

            if (validOrder && limitType && isActiveLimitOrder)
            {
                //Order is valid and a limit type and is active
                SellPosition(trade);
                return;
            }

            if (validOrder && trade.Terms == "Market")
            {
                //Order is valid and a market order
                SellPosition(trade);
            }
        }

        private void SellPosition(Trade trade)
        {
            //Search owned positions for a match with the trade's ticker
            var position = _portfolioPositions.Find(p => p.Ticker == trade.Ticker);
            var securityType = position.GetSecurityType();
            var ticker = trade.Ticker;
            var shares = trade.Shares;
            
            if (shares == position.SharesOwned)
            {
                //User selling all shares, so find and remove the security from portfolio
                var securityToRemove = _portfolioSecurities.Find(s => s.Ticker == ticker);
                _portfolioSecurities.Remove(securityToRemove);

                //Find and remove all taxlots
                var originalTaxLots = new List<Taxlot>(_portfolioTaxlots);
                var taxlotsToRemove = originalTaxLots.Where(t => t.Ticker == ticker);
                foreach (var lot in taxlotsToRemove)
                {
                    _portfolioTaxlots.Remove(lot);
                }

                //Remove the position
                _portfolioPositions.Remove(position);
            }            
            else if (shares > position.SharesOwned)
            {
                //User trying to sell too many shares
                var message = new TradeMessage() { Shares = shares, Ticker = ticker, Message = "Order quantity exceeds shares owned!" };
                Messenger.Default.Send(message);
            }
            else 
            {
                //User selling partial position
                position.SellShares(shares);
            }
        }

        /// <summary>
        /// During trade execution, checks for a limit order and
        /// whether it is active or not.
        /// </summary>
        /// <param name="trade"></param>
        /// <returns></returns>
        private bool CheckOrderLimit(Trade trade)
        {
            var buyOrSell = trade.BuyOrSell;
            var terms = trade.Terms;
            var security = trade.Security;
            var limit = trade.Limit;

            //Buy Order validation
            if (buyOrSell == "Buy" && terms == "Limit" && security.LastPrice <= limit)
            {
                return true;
            }

            if (buyOrSell == "Buy" && terms == "Limit" && security.LastPrice >= limit)
            {
                return false;
            }

            //Sell Order validation
            if (buyOrSell == "Sell" && terms == "Limit" && security.LastPrice <= limit)
            {
                return true;
            }

            if (buyOrSell == "Sell" && terms == "Limit" && security.LastPrice >= limit)
            {
                return false;
            }

            return false;
        }

        /// <summary>
        /// Gets updated pricing for all tickers in the LimitOrderList,
        /// then compares the limits to the prices & Buy or Sell terms.
        /// If last price is valid vs. the limit, proceed with trade.
        /// </summary>
        private void CheckLimitOrdersForActive()
        {
            var securitiesToCheck = new List<Security>();
            var completedLimitOrders = new List<LimitOrder>();

            foreach (var order in LimitOrderList)
            {
                if(order.SecurityType is Stock)
                    securitiesToCheck.Add(new Stock("", order.Ticker, "", 0, 0));
                else if (order.SecurityType is MutualFund)
                    securitiesToCheck.Add(new MutualFund("", order.Ticker, "", 0, 0));
            }

            _stockDataService.GetUpdatedPricing(securitiesToCheck);

            foreach (var sec in securitiesToCheck)
            {
                //Get all limit orders for the security being iterated
                var matches = LimitOrderList.Where(s => s.Ticker == sec.Ticker);
                
                foreach (var match in matches)
                {
                    var securityType = match.SecurityType;
                    var isActive = match.IsLimitOrderActive(sec.LastPrice);

                    if (isActive && match.TradeType == "Sell" && securityType is Stock)
                    {
                        var securityToTrade = new Stock("", sec.Ticker, sec.Description, sec.LastPrice, sec.Yield);
                        var newTrade = new Trade(match.TradeType, securityToTrade, match.Ticker, match.Shares, "Limit", match.Limit, match.OrderDuration);
                        SellPosition(newTrade);
                        completedLimitOrders.Add(match);
                    }
                    else if (isActive && match.TradeType == "Buy" && securityType is Stock)
                    {
                        var securityToTrade = new Stock("", sec.Ticker, sec.Description, sec.LastPrice, sec.Yield);
                        var newTrade = new Trade(match.TradeType, securityToTrade, match.Ticker, match.Shares, "Limit", match.Limit, match.OrderDuration);                        
                        AddPosition(newTrade);
                        completedLimitOrders.Add(match);
                    }
                    else if (isActive && match.TradeType == "Sell" && securityType is MutualFund)
                    {
                        var securityToTrade = new MutualFund("", sec.Ticker, sec.Description, sec.LastPrice, sec.Yield);
                        var newTrade = new Trade(match.TradeType, securityToTrade, match.Ticker, match.Shares, "Limit", match.Limit, match.OrderDuration);
                        SellPosition(newTrade);
                        completedLimitOrders.Add(match);
                    }
                    else if (isActive && match.TradeType == "Buy" && securityType is MutualFund)
                    {
                        var securityToTrade = new MutualFund("", sec.Ticker, sec.Description, sec.LastPrice, sec.Yield);
                        var newTrade = new Trade(match.TradeType, securityToTrade, match.Ticker, match.Shares, "Limit", match.Limit, match.OrderDuration);
                        AddPosition(newTrade);
                        completedLimitOrders.Add(match);
                    }
                }
            }

            foreach (var order in completedLimitOrders)
            {
                LimitOrderList.Remove(order);
            }
        }

        /// <summary>
        /// Will be called by the security screener
        /// </summary>
        /// <param name="ticker"></param>
        /// <returns></returns>
        public Security GetTradePreviewSecurity(string ticker)
        {
            var securityToReturn = _stockDataService.GetSecurityInfo(ticker);
            if (securityToReturn is Stock)
                return (Stock)securityToReturn;
            if (securityToReturn is MutualFund)
                return (MutualFund)securityToReturn;

            //Should not hit this.
            return new Stock("", "XXX", "Unknown Stock", 0, 0.00);
        }

        /// <summary>
        /// Will be called through the order entry system, where a security type
        /// must be selected to proceed
        /// </summary>
        /// <param name="ticker"></param>
        /// <param name="securityType"></param>
        /// <returns></returns>
        public Security GetTradePreviewSecurity(string ticker, Security securityType)
        {
            var securityToReturn = _stockDataService.GetSecurityInfo(ticker);
            if (securityToReturn is Stock)
                return (Stock)securityToReturn;
            else if (securityToReturn is MutualFund)
                return (MutualFund)securityToReturn;
            else return new Stock("", "XXX", "Unknown Stock", 0, 0.00);
        }

        /// <summary>
        /// Returns PositionsByWeight for all securities
        /// </summary>
        /// <returns></returns>
        public ObservableCollection<PositionByWeight> GetChartAllSecurities()
        {
            decimal totalValue = 0;
            var positionsByWeight = new ObservableCollection<PositionByWeight>();

            foreach (var pos in _portfolioPositions)
            {
                totalValue += pos.MarketValue;
            }

            foreach (var pos in _portfolioPositions)
            {
                var weight = (pos.MarketValue / totalValue) * 100;
                positionsByWeight.Add(new PositionByWeight(pos.Ticker, Math.Round(weight, 2)));
            }

            return positionsByWeight;
        }

        /// <summary>
        /// Returns PositionsByWeight for stocks only
        /// </summary>
        /// <returns></returns>
        public ObservableCollection<PositionByWeight> GetChartStocksOnly()
        {
            decimal totalValue = 0;
            var positionsByWeight = new ObservableCollection<PositionByWeight>();

            foreach (var pos in _portfolioPositions.Where(s => s.Security is Stock))
            {
                totalValue += pos.MarketValue;
            }

            foreach (var pos in _portfolioPositions.Where(s => s.Security is Stock))
            {
                var weight = (pos.MarketValue / totalValue) * 100;
                positionsByWeight.Add(new PositionByWeight(pos.Ticker, Math.Round(weight, 2)));
            }

            return positionsByWeight;
        }

        /// <summary>
        /// Returns PositionsByWeight for mutual funds only
        /// </summary>
        /// <returns></returns>
        public ObservableCollection<PositionByWeight> GetChartFundsOnly()
        {
            decimal totalValue = 0;
            var positionsByWeight = new ObservableCollection<PositionByWeight>();

            foreach (var pos in _portfolioPositions.Where(s => s.Security is MutualFund))
            {
                totalValue += pos.MarketValue;
            }

            foreach (var pos in _portfolioPositions.Where(s => s.Security is MutualFund))
            {
                var weight = (pos.MarketValue / totalValue) * 100;
                positionsByWeight.Add(new PositionByWeight(pos.Ticker, Math.Round(weight, 2)));
            }

            return positionsByWeight;
        }

        /// <summary>
        /// When timer ticks, StockDataService uses YahooAPIService to update pricing 
        /// information for all securities in the list, then updates the security list
        /// in this class and sends out the PortfolioMessage
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void _timer_Tick(object sender, EventArgs e)
        {
            UpdatePortfolioPrices();
            CheckLimitOrdersForActive();
        }

        public void UpdateTimerInterval(TimeSpan timespan)
        {
            var clockIsRunning = _timer.IsEnabled;

            _timer.Stop();
            _timer.Interval = timespan;

            if (clockIsRunning)
                _timer.Start();
        }

        public void UpdatePortfolioPrices()
        {
            //Update securities' pricing data
            _stockDataService.GetUpdatedPricing(_portfolioSecurities);
        }

        /// <summary>
        /// Starts the 10-second-interval update timer
        /// </summary>
        public void StartUpdates()
        {
            _timer.Start();
        }

        /// <summary>
        /// Stops the 10-second-interval update timer
        /// </summary>
        public void StopUpdates()
        {
            _timer.Stop();
        }

        public List<Position> GetPositions()
        {
            return _portfolioPositions;
        }

        public List<Taxlot> GetTaxlots()
        {
            return _portfolioTaxlots;
        }

        public List<LimitOrder> GetLimitOrders()
        {
            return LimitOrderList;
        }


        /// <summary>
        /// For a Sale, checks with list of user positions to make sure security types match
        /// For a buy, pulls the security info from StockDataService
        /// </summary>
        /// <param name="ticker"></param>
        /// <param name="tradeType"></param>
        /// <returns></returns>
        public Security GetSecurityType(string ticker, string tradeType)
        {
            Security secType;

            if (tradeType == "Sell")
                secType = _portfolioPositions.Find(s => s.Ticker == ticker).GetSecurityType();
            else
            {
                secType = _stockDataService.GetSecurityInfo(ticker);
            }

            return secType;
        }

        public void UploadAllDatabases()
        {
            UploadSecurityDatabase();
            UploadPortfolio();
            UploadLimitOrdersToDatabase();
        }

        private void UploadSecurityDatabase()
        {
            _stockDataService.UploadSecuritiesToDatabase();
        }

        public void UploadPortfolio()
        {
            _portfolioDatabaseService.SavePortfolioToDatabase();
        }

        public void UploadLimitOrdersToDatabase()
        {
            _portfolioDatabaseService.UploadLimitOrdersToDatabase(LimitOrderList);
        }

        public void DeletePortfolio()
        {
            _portfolioSecurities.Clear();
            _portfolioTaxlots.Clear();
            _portfolioDatabaseService.DeletePortfolio(_portfolioPositions);
        }

        /// <summary>
        /// For testing only.
        /// </summary>
        public void TestLimitOrderMethods()
        {
            UpdatePortfolioPrices();
            CheckLimitOrdersForActive();
        }

    }
}
