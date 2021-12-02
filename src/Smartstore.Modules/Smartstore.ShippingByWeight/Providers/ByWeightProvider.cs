﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Smartstore.Core;
using Smartstore.Core.Catalog.Pricing;
using Smartstore.Core.Catalog.Products;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Checkout.Shipping;
using Smartstore.Core.Checkout.Tax;
using Smartstore.Core.Common.Services;
using Smartstore.Core.Data;
using Smartstore.Core.Localization;
using Smartstore.Core.Stores;
using Smartstore.Engine.Modularity;
using Smartstore.Http;
using Smartstore.ShippingByWeight;
using Smartstore.ShippingByWeight.Settings;
using Smartstore.Utilities;

namespace Smartstore.Shipping
{
    [SystemName("Smartstore.ShippingByWeight")]
    [FriendlyName("Shipping by weight")]
    [Order(0)]
    internal class ByWeightProvider : IShippingRateComputationMethod, IConfigurable
    {
        private readonly SmartDbContext _db;
        private readonly ICommonServices _services;
        private readonly IShippingService _shippingService;
        private readonly IPriceCalculationService _priceCalculationService;
        private readonly IProductService _productService;
        private readonly ICurrencyService _currencyService;
        private readonly IStoreContext _storeContext;
        private readonly ShippingByWeightSettings _shippingByWeightSettings;

        public ByWeightProvider(SmartDbContext db,
            ICommonServices services,
            IShippingService shippingService,
            IPriceCalculationService priceCalculationService,
            IProductService productService,
            ICurrencyService currencyService,
            IStoreContext storeContext,
            ShippingByWeightSettings shippingByWeightSettings)
        {
            _db = db;
            _services = services;
            _shippingService = shippingService;
            _priceCalculationService = priceCalculationService;
            _productService = productService;
            _currencyService = currencyService;
            _storeContext = storeContext;
            _shippingByWeightSettings = shippingByWeightSettings;
            
            T = NullLocalizer.Instance;
        }

        public Localizer T { get; set; }

        /// <summary>
        /// Gets the rate for the shipping method based on total weight of the order.
        /// </summary>
        /// <param name="subtotal">The order's subtotal</param>
        /// <param name="weight">The order's weight</param>
        /// <param name="shippingMethodId">Shipping method identifier</param>
        /// <param name="countryId">Country identifier</param>
        /// <param name="zip">Zip code</param>
        /// <returns>The rate for the shipping method.</returns>
		private async Task<decimal?> GetRateAsync(decimal subtotal, decimal weight, int shippingMethodId, int storeId, int countryId, string zip)
        {
            decimal? shippingTotal = null;

            zip = zip.EmptyNull().Trim();

            var shippingByWeightRecords = await _db.ShippingRatesByWeight()
                .Where(x => x.StoreId == storeId || x.StoreId == 0)
                .Where(x => x.ShippingMethodId == shippingMethodId)
                .ApplyWeightFilter(weight)
                .ApplyRegionFilter(countryId, zip)
                .ToListAsync();

            var shippingByWeightRecord = shippingByWeightRecords
                .Where(x => (zip.IsEmpty() && x.Zip.IsEmpty()) || ZipMatches(zip, x.Zip))
                .LastOrDefault();

            if (shippingByWeightRecord == null)
            {
                return _shippingByWeightSettings.LimitMethodsToCreated ? null : decimal.Zero;
            }

            if (shippingByWeightRecord.UsePercentage && shippingByWeightRecord.ShippingChargePercentage <= decimal.Zero)
            {
                return decimal.Zero;
            }
            
            if (!shippingByWeightRecord.UsePercentage && shippingByWeightRecord.ShippingChargeAmount <= decimal.Zero)
            {
                return decimal.Zero;
            }

            if (shippingByWeightRecord.UsePercentage)
            {
                shippingTotal = Math.Round((decimal)(((float)subtotal) * ((float)shippingByWeightRecord.ShippingChargePercentage) / 100f), 2);
            }
            else
            {
                shippingTotal = _shippingByWeightSettings.CalculatePerWeightUnit ? shippingByWeightRecord.ShippingChargeAmount * weight : shippingByWeightRecord.ShippingChargeAmount;
            }

            if (shippingTotal < decimal.Zero)
            {
                shippingTotal = decimal.Zero;
            }

            return shippingTotal;
        }

        private static bool ZipMatches(string zip, string pattern)
        {
            if (pattern.IsEmpty() || pattern == "*")
            {
                return true; // catch all
            }

            var patterns = pattern.Contains(',')
                ? pattern.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim())
                : new string[] { pattern };

            try
            {
                foreach (var entry in patterns)
                {
                    var wildcard = new Wildcard(entry, true);
                    if (wildcard.IsMatch(zip))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return zip.EqualsNoCase(pattern);
            }

            return false;
        }

        public RouteInfo GetConfigurationRoute()
            => new("Configure", "ShippingByWeight", new { area = "Admin" });

        public Task<decimal?> GetFixedRateAsync(ShippingOptionRequest request) 
            => Task.FromResult<decimal?>(null);

        public async Task<ShippingOptionResponse> GetShippingOptionsAsync(ShippingOptionRequest request)
        {
            Guard.NotNull(request, nameof(request));

            var response = new ShippingOptionResponse();

            if (request.Items == null || request.Items.Count == 0)
            {
                response.Errors.Add(T("Admin.System.Warnings.NoShipmentItems"));
                return response;
            }

            int storeId = request.StoreId > 0 ? request.StoreId : _storeContext.CurrentStore.Id;
            decimal subTotalInclTax = decimal.Zero;
            decimal subTotalExclTax = decimal.Zero;
            decimal currentSubTotal = decimal.Zero;
            int countryId = 0;
            string zip = null;

            if (request.ShippingAddress != null)
            {
                countryId = request.ShippingAddress.CountryId ?? 0;
                zip = request.ShippingAddress.ZipPostalCode;
            }

            var allProducts = request.Items
                .Select(x => x.Item.Product)
                .Union(request.Items.Select(x => x.ChildItems).SelectMany(child => child.Select(x => x.Item.Product)))
                .ToArray();

            var store = await _db.Stores.FindByIdAsync(request.StoreId, false);
            var batchContext = _productService.CreateProductBatchContext(allProducts, store, request.Customer, false);
            var calculationOptions = _priceCalculationService.CreateDefaultOptions(false, request.Customer, null, batchContext);
            calculationOptions.IgnoreDiscounts = false;
            calculationOptions.TaxInclusive = true;

            foreach (var shoppingCartItem in request.Items)
            {
                if (shoppingCartItem.Item.IsFreeShipping || !shoppingCartItem.Item.IsShippingEnabled)
                {
                    continue;
                }

                var calculationContext = await _priceCalculationService.CreateCalculationContextAsync(shoppingCartItem, calculationOptions);
                var (unitPrice, itemSubtotal) = await _priceCalculationService.CalculateSubtotalAsync(calculationContext);

                subTotalInclTax += itemSubtotal.Tax.Value.PriceGross;
                subTotalExclTax += itemSubtotal.Tax.Value.PriceNet;
            }

            var cart = new ShoppingCart(request.Customer, request.StoreId, request.Items);
            var weight = await _shippingService.GetCartTotalWeightAsync(cart, _shippingByWeightSettings.IncludeWeightOfFreeShippingProducts);
            var workingCurreny = _services.WorkContext.WorkingCurrency;
            var shippingMethods = await _shippingService.GetAllShippingMethodsAsync(request.StoreId);
            currentSubTotal = _services.WorkContext.TaxDisplayType == TaxDisplayType.ExcludingTax ? subTotalExclTax : subTotalInclTax;

            var shippingByWeightRecords = await _db.ShippingRatesByWeight()
                .Where(x => x.StoreId == storeId || x.StoreId == 0)
                .ApplyWeightFilter(weight)
                .ApplyRegionFilter(countryId, zip)
                .ToListAsync();

            foreach (var shippingMethod in shippingMethods)
            {
                var record = shippingByWeightRecords
                    .Where(x => x.ShippingMethodId == shippingMethod.Id)
                    .Where(x => ZipMatches(zip, x.Zip))
                    .LastOrDefault();

                decimal? rate = await GetRateAsync(subTotalInclTax, weight, shippingMethod.Id, request.StoreId, countryId, zip);
                if (rate.HasValue)
                {
                    var shippingOption = new ShippingOption();
                    shippingOption.ShippingMethodId = shippingMethod.Id;
                    shippingOption.Name = shippingMethod.GetLocalized(x => x.Name);

                    if (record != null && record.SmallQuantityThreshold > currentSubTotal)
                    {
                        string surchargeHint = T("Plugins.Shipping.ByWeight.SmallQuantitySurchargeNotReached",
                            _currencyService.ConvertToWorkingCurrency(record.SmallQuantitySurcharge).ToString(true, false, _currencyService.GetTaxFormat()),
                            _currencyService.ConvertToWorkingCurrency(record.SmallQuantitySurcharge).ToString(true, false, _currencyService.GetTaxFormat()));

                        shippingOption.Description = shippingMethod.GetLocalized(x => x.Description) + surchargeHint;
                        shippingOption.Rate = rate.Value + record.SmallQuantitySurcharge;
                    }
                    else
                    {
                        shippingOption.Description = shippingMethod.GetLocalized(x => x.Description);
                        shippingOption.Rate = rate.Value;
                    }
                    response.ShippingOptions.Add(shippingOption);
                }
            }

            return response;
        }

        /// <summary>
        /// Gets a shipping rate computation method type
        /// </summary>
        public ShippingRateComputationMethodType ShippingRateComputationMethodType => ShippingRateComputationMethodType.Offline;

        /// <summary>
        /// Gets a shipment tracker
        /// </summary>
        public IShipmentTracker ShipmentTracker =>
                //uncomment the line below to return a general shipment tracker (finds an appropriate tracker by tracking number)
                //return new GeneralShipmentTracker(EngineContext.Current.Resolve<ITypeFinder>());
                null;

        public bool IsActive => true;
    }
}
