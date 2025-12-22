// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using EasyAppDev.Blazor.Store.Security;

namespace EasyAppDev.Blazor.Store.Sample.Security;

/// <summary>
/// Demonstrates various custom validator implementations.
/// Use these patterns as templates for your own validators.
/// </summary>
public static class CustomValidatorSample
{
    #region Example State Types

    /// <summary>
    /// E-commerce order state.
    /// </summary>
    public record OrderState(
        string OrderId,
        string CustomerId,
        List<OrderItem> Items,
        decimal Subtotal,
        decimal Tax,
        decimal ShippingCost,
        decimal Total,
        ShippingAddress? ShippingAddress,
        BillingInfo? BillingInfo,
        OrderStatus Status,
        DateTimeOffset? OrderedAt);

    public record OrderItem(
        string ProductId,
        string Name,
        int Quantity,
        decimal UnitPrice,
        decimal Discount,
        decimal LineTotal);

    public record ShippingAddress(
        string Street,
        string City,
        string State,
        string PostalCode,
        string Country);

    public record BillingInfo(
        string FullName,
        [property: SensitiveData] string CardLastFour,
        string ExpiryMonth,
        string ExpiryYear,
        [property: SensitiveData] string? Cvv);

    public enum OrderStatus
    {
        Draft,
        Pending,
        Confirmed,
        Shipped,
        Delivered,
        Cancelled
    }

    #endregion

    #region Basic Validator Example

    /// <summary>
    /// Basic validator with simple validation rules.
    /// </summary>
    public class BasicOrderValidator : IStateValidator<OrderState>
    {
        public StateValidationResult Validate(OrderState state)
        {
            var errors = new List<string>();

            if (state == null)
                return StateValidationResult.Failure("State cannot be null");

            // Required field validations
            if (string.IsNullOrWhiteSpace(state.OrderId))
                errors.Add("Order ID is required");

            if (string.IsNullOrWhiteSpace(state.CustomerId))
                errors.Add("Customer ID is required");

            // Range validations
            if (state.Items == null || state.Items.Count == 0)
                errors.Add("Order must contain at least one item");

            if (state.Items?.Count > 100)
                errors.Add("Order cannot contain more than 100 items");

            // Business rule validations
            if (state.Total < 0)
                errors.Add("Total cannot be negative");

            if (state.Subtotal < 0)
                errors.Add("Subtotal cannot be negative");

            return errors.Count > 0
                ? StateValidationResult.Failure(errors)
                : StateValidationResult.Success();
        }
    }

    #endregion

    #region Comprehensive Validator Example

    /// <summary>
    /// Comprehensive validator with deep validation and business rules.
    /// </summary>
    public class ComprehensiveOrderValidator : IStateValidator<OrderState>
    {
        private const int MaxItemQuantity = 1000;
        private const decimal MaxOrderTotal = 100_000m;
        private const int MaxAddressLength = 200;

        public StateValidationResult Validate(OrderState state)
        {
            var errors = new List<string>();

            if (state == null)
                return StateValidationResult.Failure("State cannot be null");

            ValidateOrderId(state.OrderId, errors);
            ValidateCustomerId(state.CustomerId, errors);
            ValidateItems(state.Items, errors);
            ValidateTotals(state, errors);
            ValidateShippingAddress(state.ShippingAddress, errors);
            ValidateBillingInfo(state.BillingInfo, errors);
            ValidateBusinessRules(state, errors);

            return errors.Count > 0
                ? StateValidationResult.Failure(errors)
                : StateValidationResult.Success();
        }

        private void ValidateOrderId(string? orderId, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                errors.Add("Order ID is required");
                return;
            }

            // Validate format (e.g., ORD-XXXXXXXX)
            if (!Regex.IsMatch(orderId, @"^ORD-[A-Z0-9]{8}$"))
                errors.Add("Order ID must be in format ORD-XXXXXXXX");
        }

        private void ValidateCustomerId(string? customerId, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(customerId))
            {
                errors.Add("Customer ID is required");
                return;
            }

            if (customerId.Length > 50)
                errors.Add("Customer ID exceeds maximum length");

            // Check for valid GUID format
            if (!Guid.TryParse(customerId, out _))
                errors.Add("Customer ID must be a valid GUID");
        }

        private void ValidateItems(List<OrderItem>? items, List<string> errors)
        {
            if (items == null || items.Count == 0)
            {
                errors.Add("Order must contain at least one item");
                return;
            }

            if (items.Count > 100)
            {
                errors.Add("Order cannot contain more than 100 items");
                return;
            }

            var seenProductIds = new HashSet<string>();

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var prefix = $"Item [{i + 1}]";

                // Required fields
                if (string.IsNullOrWhiteSpace(item.ProductId))
                    errors.Add($"{prefix}: Product ID is required");
                else if (!seenProductIds.Add(item.ProductId))
                    errors.Add($"{prefix}: Duplicate product ID '{item.ProductId}'");

                if (string.IsNullOrWhiteSpace(item.Name))
                    errors.Add($"{prefix}: Product name is required");

                // Range validations
                if (item.Quantity <= 0)
                    errors.Add($"{prefix}: Quantity must be at least 1");
                else if (item.Quantity > MaxItemQuantity)
                    errors.Add($"{prefix}: Quantity cannot exceed {MaxItemQuantity}");

                if (item.UnitPrice < 0)
                    errors.Add($"{prefix}: Unit price cannot be negative");

                if (item.Discount < 0)
                    errors.Add($"{prefix}: Discount cannot be negative");

                if (item.Discount > item.UnitPrice * item.Quantity)
                    errors.Add($"{prefix}: Discount cannot exceed line total");

                // Line total validation
                var expectedLineTotal = (item.UnitPrice * item.Quantity) - item.Discount;
                if (Math.Abs(item.LineTotal - expectedLineTotal) > 0.01m)
                    errors.Add($"{prefix}: Line total calculation mismatch");
            }
        }

        private void ValidateTotals(OrderState state, List<string> errors)
        {
            if (state.Subtotal < 0)
                errors.Add("Subtotal cannot be negative");

            if (state.Tax < 0)
                errors.Add("Tax cannot be negative");

            if (state.ShippingCost < 0)
                errors.Add("Shipping cost cannot be negative");

            if (state.Total < 0)
                errors.Add("Total cannot be negative");

            if (state.Total > MaxOrderTotal)
                errors.Add($"Total cannot exceed {MaxOrderTotal:C}");

            // Verify total calculation
            if (state.Items != null && state.Items.Count > 0)
            {
                var calculatedSubtotal = state.Items.Sum(i => i.LineTotal);
                if (Math.Abs(state.Subtotal - calculatedSubtotal) > 0.01m)
                    errors.Add("Subtotal does not match sum of line totals");

                var expectedTotal = state.Subtotal + state.Tax + state.ShippingCost;
                if (Math.Abs(state.Total - expectedTotal) > 0.01m)
                    errors.Add("Total calculation mismatch");
            }
        }

        private void ValidateShippingAddress(ShippingAddress? address, List<string> errors)
        {
            if (address == null)
                return; // Address optional in draft state

            if (string.IsNullOrWhiteSpace(address.Street))
                errors.Add("Shipping street address is required");
            else if (address.Street.Length > MaxAddressLength)
                errors.Add("Shipping street address exceeds maximum length");

            if (string.IsNullOrWhiteSpace(address.City))
                errors.Add("Shipping city is required");
            else if (address.City.Length > 100)
                errors.Add("Shipping city exceeds maximum length");

            if (string.IsNullOrWhiteSpace(address.PostalCode))
                errors.Add("Shipping postal code is required");
            else if (!IsValidPostalCode(address.PostalCode, address.Country))
                errors.Add("Invalid shipping postal code format");

            if (string.IsNullOrWhiteSpace(address.Country))
                errors.Add("Shipping country is required");
            else if (!IsValidCountryCode(address.Country))
                errors.Add("Invalid shipping country code");
        }

        private void ValidateBillingInfo(BillingInfo? billing, List<string> errors)
        {
            if (billing == null)
                return; // Billing optional in draft state

            if (string.IsNullOrWhiteSpace(billing.FullName))
                errors.Add("Billing name is required");
            else if (billing.FullName.Length > 100)
                errors.Add("Billing name exceeds maximum length");

            if (string.IsNullOrWhiteSpace(billing.CardLastFour))
                errors.Add("Card last four digits required");
            else if (!Regex.IsMatch(billing.CardLastFour, @"^\d{4}$"))
                errors.Add("Card last four must be exactly 4 digits");

            if (!IsValidExpiryDate(billing.ExpiryMonth, billing.ExpiryYear))
                errors.Add("Invalid or expired card expiry date");
        }

        private void ValidateBusinessRules(OrderState state, List<string> errors)
        {
            // Status-specific validations
            switch (state.Status)
            {
                case OrderStatus.Confirmed:
                case OrderStatus.Shipped:
                case OrderStatus.Delivered:
                    // Orders in these statuses must have complete info
                    if (state.ShippingAddress == null)
                        errors.Add("Order must have shipping address");
                    if (state.BillingInfo == null)
                        errors.Add("Order must have billing info");
                    if (state.OrderedAt == null)
                        errors.Add("Order must have order timestamp");
                    break;
            }

            // Date validations
            if (state.OrderedAt.HasValue)
            {
                if (state.OrderedAt.Value > DateTimeOffset.UtcNow.AddMinutes(5))
                    errors.Add("Order timestamp cannot be in the future");

                if (state.OrderedAt.Value < DateTimeOffset.UtcNow.AddYears(-10))
                    errors.Add("Order timestamp is too old");
            }
        }

        private static bool IsValidPostalCode(string postalCode, string country)
        {
            return country?.ToUpperInvariant() switch
            {
                "US" => Regex.IsMatch(postalCode, @"^\d{5}(-\d{4})?$"),
                "CA" => Regex.IsMatch(postalCode, @"^[A-Z]\d[A-Z]\s?\d[A-Z]\d$", RegexOptions.IgnoreCase),
                "UK" or "GB" => Regex.IsMatch(postalCode, @"^[A-Z]{1,2}\d[A-Z\d]?\s?\d[A-Z]{2}$", RegexOptions.IgnoreCase),
                _ => postalCode.Length is >= 3 and <= 10
            };
        }

        private static bool IsValidCountryCode(string country)
        {
            // ISO 3166-1 alpha-2 format
            return Regex.IsMatch(country, @"^[A-Z]{2}$");
        }

        private static bool IsValidExpiryDate(string? month, string? year)
        {
            if (string.IsNullOrWhiteSpace(month) || string.IsNullOrWhiteSpace(year))
                return false;

            if (!int.TryParse(month, out var m) || m < 1 || m > 12)
                return false;

            if (!int.TryParse(year, out var y))
                return false;

            // Handle 2-digit year
            if (y < 100)
                y += 2000;

            var expiryDate = new DateTime(y, m, 1).AddMonths(1).AddDays(-1);
            return expiryDate >= DateTime.Today;
        }
    }

    #endregion

    #region Configurable Validator Example

    /// <summary>
    /// Validator with configurable rules via options.
    /// </summary>
    public class ConfigurableOrderValidator : IStateValidator<OrderState>
    {
        private readonly OrderValidationOptions _options;

        public ConfigurableOrderValidator(OrderValidationOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public StateValidationResult Validate(OrderState state)
        {
            var errors = new List<string>();

            if (state == null)
                return StateValidationResult.Failure("State cannot be null");

            // Configurable validations
            if (_options.RequireOrderId && string.IsNullOrWhiteSpace(state.OrderId))
                errors.Add("Order ID is required");

            if (state.Items?.Count > _options.MaxItems)
                errors.Add($"Order cannot contain more than {_options.MaxItems} items");

            if (state.Total > _options.MaxOrderTotal)
                errors.Add($"Order total cannot exceed {_options.MaxOrderTotal:C}");

            if (_options.RequireShippingForStatus.Contains(state.Status) &&
                state.ShippingAddress == null)
                errors.Add($"Shipping address required for {state.Status} status");

            // Custom validators
            foreach (var customValidator in _options.CustomValidators)
            {
                var error = customValidator(state);
                if (!string.IsNullOrEmpty(error))
                    errors.Add(error);
            }

            return errors.Count > 0
                ? StateValidationResult.Failure(errors)
                : StateValidationResult.Success();
        }
    }

    /// <summary>
    /// Options for configurable validator.
    /// </summary>
    public class OrderValidationOptions
    {
        public bool RequireOrderId { get; set; } = true;
        public int MaxItems { get; set; } = 100;
        public decimal MaxOrderTotal { get; set; } = 100_000m;
        public HashSet<OrderStatus> RequireShippingForStatus { get; set; } = new()
        {
            OrderStatus.Confirmed,
            OrderStatus.Shipped,
            OrderStatus.Delivered
        };
        public List<Func<OrderState, string?>> CustomValidators { get; set; } = new();
    }

    #endregion

    #region Async Validator Example

    /// <summary>
    /// Validator that performs async checks (e.g., database lookups).
    /// Note: IStateValidator is synchronous, so wrap async calls carefully.
    /// </summary>
    public class AsyncAwareOrderValidator : IStateValidator<OrderState>
    {
        private readonly IProductCatalog _catalog;
        private readonly ICustomerService _customerService;

        public AsyncAwareOrderValidator(
            IProductCatalog catalog,
            ICustomerService customerService)
        {
            _catalog = catalog;
            _customerService = customerService;
        }

        public StateValidationResult Validate(OrderState state)
        {
            // For sync interface, use synchronous validation
            // For async validation, create a separate async method
            var errors = new List<string>();

            if (state == null)
                return StateValidationResult.Failure("State cannot be null");

            // Sync validations
            if (state.Items == null || state.Items.Count == 0)
                errors.Add("Order must contain at least one item");

            // For async validations, check cached data or defer
            // Do NOT block with .Result or .GetAwaiter().GetResult()

            return errors.Count > 0
                ? StateValidationResult.Failure(errors)
                : StateValidationResult.Success();
        }

        /// <summary>
        /// Async validation method - call separately when async validation needed.
        /// </summary>
        public async Task<StateValidationResult> ValidateAsync(
            OrderState state,
            CancellationToken cancellationToken = default)
        {
            var errors = new List<string>();

            if (state == null)
                return StateValidationResult.Failure("State cannot be null");

            // Validate products exist
            if (state.Items != null)
            {
                var productIds = state.Items.Select(i => i.ProductId).ToList();
                var existingProducts = await _catalog.GetProductsAsync(productIds, cancellationToken);

                foreach (var item in state.Items)
                {
                    if (!existingProducts.ContainsKey(item.ProductId))
                        errors.Add($"Product '{item.ProductId}' not found");
                }
            }

            // Validate customer
            var customerValid = await _customerService.CustomerExistsAsync(
                state.CustomerId, cancellationToken);
            if (!customerValid)
                errors.Add("Customer not found");

            return errors.Count > 0
                ? StateValidationResult.Failure(errors)
                : StateValidationResult.Success();
        }
    }

    /// <summary>
    /// Product catalog interface.
    /// </summary>
    public interface IProductCatalog
    {
        Task<Dictionary<string, ProductInfo>> GetProductsAsync(
            List<string> productIds,
            CancellationToken cancellationToken);
    }

    public record ProductInfo(string ProductId, string Name, decimal Price, bool InStock);

    /// <summary>
    /// Customer service interface.
    /// </summary>
    public interface ICustomerService
    {
        Task<bool> CustomerExistsAsync(string customerId, CancellationToken cancellationToken);
    }

    #endregion
}

/// <summary>
/// Example of registering validators with DI.
/// </summary>
public static class ValidatorRegistration
{
    public static IServiceCollection AddOrderValidators(this IServiceCollection services)
    {
        // Register basic validator
        services.AddStateValidator<CustomValidatorSample.OrderState, CustomValidatorSample.BasicOrderValidator>();

        // Or register comprehensive validator
        // services.AddStateValidator<OrderState, ComprehensiveOrderValidator>();

        // Or register configurable validator with options
        services.AddSingleton(new CustomValidatorSample.OrderValidationOptions
        {
            MaxItems = 50,
            MaxOrderTotal = 50_000m,
            CustomValidators =
            {
                state => state.Items?.Any(i => i.Quantity > 10) == true
                    ? "Single item quantity cannot exceed 10"
                    : null
            }
        });
        services.AddStateValidator<CustomValidatorSample.OrderState, CustomValidatorSample.ConfigurableOrderValidator>();

        return services;
    }
}
