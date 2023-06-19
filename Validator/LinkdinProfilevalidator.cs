using FluentValidation;
using Scrapping_Linkdin.Models.Request;

public class LinkdinProfileValidator : AbstractValidator<LinkdinProfile>
{
    public LinkdinProfileValidator()
    {
        //RuleFor(x => x.PartitionKey).NotEmpty();
        RuleFor(x => x.ProfileId).NotEmpty().Must(BeAValidUrl).WithMessage("Invalid URL format");//Must method is used to apply a custom validation rule
    }//BeAValidUrl is a custom validation rule used in conjunction with FluentValidation library


    private bool BeAValidUrl(string url)
    {
        // Implement your custom URL validation logic here
        // Return true if the URL is valid; otherwise, return false
        return Uri.TryCreate(url, UriKind.Absolute, out _);
    }
}