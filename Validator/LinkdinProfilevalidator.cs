using FluentValidation;
using Scrapping_Linkdin.Models.Request;

namespace Scrapping_Linkdin.Validator
{
    public class LinkdinProfileValidator:AbstractValidator<LinkdinProfile>
    {
        public LinkdinProfileValidator()
        {
                RuleFor(x=>x.ProfileId).NotNull().NotEmpty();
        }

    }
}
