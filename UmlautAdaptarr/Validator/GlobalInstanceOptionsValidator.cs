using System.Net;
using FluentValidation;
using UmlautAdaptarr.Options.ArrOptions.InstanceOptions;

namespace UmlautAdaptarr.Validator;

public class GlobalInstanceOptionsValidator : AbstractValidator<GlobalInstanceOptions>
{
    public GlobalInstanceOptionsValidator()
    {
        RuleFor(x => x.Enabled).NotNull();

        When(x => x.Enabled, () =>
        {
            RuleFor(x => x.Host)
                .NotEmpty().WithMessage("Host is required when Enabled is true.")
                .Must(BeAValidUrl).WithMessage("Host/Url must start with http:// or https:// and be a valid address.")
                .Must(BeReachable)
                .WithMessage("Host/Url is not reachable. Please check your Host or your UmlautAdaptrr Settings");

            RuleFor(x => x.ApiKey)
                .NotEmpty().WithMessage("ApiKey is required when Enabled is true.");
        });
    }

    private bool BeAValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
               && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }

    private static bool BeReachable(string url)
    {
        var endTime = DateTime.Now.AddMinutes(3);
        var reachable = false;

        while (DateTime.Now < endTime)
        {
            try
            {
                // TODO use HttpClient here
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.AllowAutoRedirect = false;
                request.Timeout = 3000;
                using var response = (HttpWebResponse)request.GetResponse();
                reachable = response.StatusCode == HttpStatusCode.OK;
                if (reachable)
                {
                    break;
                }
                // If status is 301/302 (Found), follow the redirect manually
                else if (response.StatusCode == HttpStatusCode.MovedPermanently || response.StatusCode == HttpStatusCode.Found)
                {
                    var redirectUrl = response.Headers["Location"];  // Get the redirect URL
                    if (!string.IsNullOrEmpty(redirectUrl))
                    {
                        // Create a new request for the redirected URL
                        var redirectRequest = (HttpWebRequest)WebRequest.Create(redirectUrl);
                        redirectRequest.Timeout = 3000;
                        using var redirectResponse = (HttpWebResponse)redirectRequest.GetResponse();
                        reachable = redirectResponse.StatusCode == HttpStatusCode.OK;
                    }
                }
            }
            catch
            {

            }

            // Wait for 15 seconds for next try
            Console.WriteLine($"The URL \"{url}\" is not reachable. Next attempt in 15 seconds...");
            Thread.Sleep(15000);
        }

        return reachable;
    }

}