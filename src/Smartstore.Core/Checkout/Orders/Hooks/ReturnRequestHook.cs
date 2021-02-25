﻿using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Smartstore.Core.Data;
using Smartstore.Data.Hooks;
using Smartstore.Events;

namespace Smartstore.Core.Checkout.Orders
{
    public class ReturnRequestHook : AsyncDbSaveHook<ReturnRequest>
    {
        private readonly SmartDbContext _db;
        private readonly IEventPublisher _eventPublisher;

        public ReturnRequestHook(SmartDbContext db, IEventPublisher eventPublisher)
        {
            _db = db;
            _eventPublisher = eventPublisher;
        }

        protected override Task<HookResult> OnDeletedAsync(ReturnRequest entity, IHookedEntity entry, CancellationToken cancelToken)
            => Task.FromResult(HookResult.Ok);

        public override async Task OnAfterSaveCompletedAsync(IEnumerable<IHookedEntity> entries, CancellationToken cancelToken)
        {
            var returnRequests = entries
                .Select(x => x.Entity)
                .OfType<ReturnRequest>()
                .ToList();

            var orderItemIds = returnRequests
                .Select(x => x.OrderItemId)
                .Distinct()
                .ToArray();

            if (orderItemIds.Any())
            {
                var orders = await _db.OrderItems
                    .Where(x => orderItemIds.Contains(x.Id))
                    .Select(x => x.Order)
                    .ToListAsync(cancelToken);

                if (orders.Any())
                {
                    foreach (var groupedOrders in orders.GroupBy(x => x.Id))
                    {
                        await _eventPublisher.PublishOrderUpdatedAsync(groupedOrders.FirstOrDefault());
                    }
                }
            }
        }
    }
}
