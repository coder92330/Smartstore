﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Smartstore.Core.Catalog.Brands;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Data;
using Smartstore.Core.Rules;

namespace Smartstore.Core.Checkout.Rules.Impl
{
    internal class PurchasedFromManufacturerRule : IRule
    {
        private readonly SmartDbContext _db;

        public PurchasedFromManufacturerRule(SmartDbContext db)
        {
            _db = db;
        }

        public async Task<bool> MatchAsync(CartRuleContext context, RuleExpression expression)
        {
            var query = _db.Orders
                .AsNoTracking()
                .Include(x => x.OrderItems)
                    .ThenInclude(x => x.Product)
                        .ThenInclude(x => x.ProductManufacturers)
                .ApplyStandardFilter(context.Customer.Id, context.Store.Id)
                .SelectMany(x => x.OrderItems);

            if (expression.Operator == RuleOperator.In || expression.Operator == RuleOperator.NotIn)
            {
                // Find match using LINQ to Entities.
                var manuIds = expression.Value as List<int>;
                if (!(manuIds?.Any() ?? false))
                {
                    return true;
                }

                if (expression.Operator == RuleOperator.In)
                {
                    return await query.Where(oi => oi.Product.ProductManufacturers.Any(pm => manuIds.Contains(pm.ManufacturerId))).AnyAsync();
                }

                return await query.Where(oi => oi.Product.ProductManufacturers.Any(pm => !manuIds.Contains(pm.ManufacturerId))).AnyAsync();
            }
            else
            {
                // Find match using LINQ to Objects.
                var manuIds = new HashSet<int>();
                var pager = query.ToFastPager(4000);

                while ((await pager.ReadNextPageAsync(x => new { x.Id, ManufacturerIds = x.Product.ProductManufacturers.Select(pm => pm.ManufacturerId) }, x => x.Id)).Out(out var orderItems))
                {
                    manuIds.AddRange(orderItems.SelectMany(x => x.ManufacturerIds));
                }

                var match = expression.HasListsMatch(manuIds);
                return match;
            }
        }
    }
}
