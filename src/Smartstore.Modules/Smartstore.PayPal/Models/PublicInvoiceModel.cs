﻿using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace Smartstore.PayPal.Models
{
    [LocalizedDisplay("Account.Fields.")]
    public class PublicInvoiceModel : ModelBase
    {
        [LocalizedDisplay("*DateOfBirth")]
        public int? DateOfBirthDay { get; set; }

        [LocalizedDisplay("*DateOfBirth")]
        public int? DateOfBirthMonth { get; set; }

        [LocalizedDisplay("*DateOfBirth")]
        public int? DateOfBirthYear { get; set; }

        [LocalizedDisplay("*Phone")]
        [DataType(DataType.PhoneNumber)]
        public string PhoneNumber { get; set; }
        public string DiallingCode { get; set; }
    }

    public partial class PayPalInvoiceValidator : SmartValidator<PublicInvoiceModel>
    {
        public PayPalInvoiceValidator(Localizer T)
        {
            RuleFor(x => x.DateOfBirthDay)
                .GreaterThan(0)
                .WithMessage(T("Plugins.Smartstore.PayPal.DateOfBirthDay.NotNull"));
            RuleFor(x => x.DateOfBirthMonth)
                .GreaterThan(0)
                .WithMessage(T("Plugins.Smartstore.PayPal.DateOfBirthMonth.NotNull"));
            RuleFor(x => x.DateOfBirthYear)
                .GreaterThan(0)
                .WithMessage(T("Plugins.Smartstore.PayPal.DateOfBirthYear.NotNull"));

            RuleFor(x => x.PhoneNumber)
                .Matches(@"^[0-9]{1,14}?$")
                .WithMessage(T("Plugins.Smartstore.PayPal.PhoneNumber.Invalid"));
        }
    }
}