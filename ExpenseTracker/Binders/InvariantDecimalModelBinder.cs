using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Globalization;

namespace ExpenseTracker.Binders
{
    public class InvariantDecimalModelBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            ValueProviderResult valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            if (valueProviderResult == ValueProviderResult.None)
            {
                return Task.CompletedTask;
            }

            bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);

            string? rawValue = valueProviderResult.FirstValue;
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                if (bindingContext.ModelType == typeof(decimal?))
                {
                    bindingContext.Result = ModelBindingResult.Success(null);
                }
                return Task.CompletedTask;
            }

            string normalizedValue = rawValue.Trim().Replace(" ", string.Empty).Replace(',', '.');

            if (decimal.TryParse(normalizedValue, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsedValue))
            {
                bindingContext.Result = ModelBindingResult.Success(parsedValue);
                return Task.CompletedTask;
            }

            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "Podaj poprawną kwotę.");
            return Task.CompletedTask;
        }
    }

    public class InvariantDecimalModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder? GetBinder(ModelBinderProviderContext context)
        {
            if (context.Metadata.ModelType == typeof(decimal) || context.Metadata.ModelType == typeof(decimal?))
            {
                return new InvariantDecimalModelBinder();
            }

            return null;
        }
    }
}