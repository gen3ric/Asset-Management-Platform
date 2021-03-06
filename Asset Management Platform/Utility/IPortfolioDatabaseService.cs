﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asset_Management_Platform.Utility
{
    public interface IPortfolioDatabaseService
    {

        List<Taxlot> GetTaxlotsFromDatabase();

        List<Position> GetPositionsFromTaxlots(List<Security> portfolioSecurities);

        void SavePortfolioToDatabase();

        void UploadLimitOrdersToDatabase(List<LimitOrder> limitOrders);

        List<LimitOrder> LoadLimitOrdersFromDatabase();

        void BackupDatabase();

        void AddToPortfolioDatabase(Position positionToAdd);

        void AddToPortfolioDatabase(Taxlot taxlotToAdd);

        void DeletePortfolio(List<Position> positions);

    }
}
