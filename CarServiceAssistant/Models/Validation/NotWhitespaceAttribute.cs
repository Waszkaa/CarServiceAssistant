using System.ComponentModel.DataAnnotations;

namespace CarServiceAssistant.ViewModels.Validation;

public sealed class NotWhitespaceAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
        => value is string s ? !string.IsNullOrWhiteSpace(s) : value is not null;
}
