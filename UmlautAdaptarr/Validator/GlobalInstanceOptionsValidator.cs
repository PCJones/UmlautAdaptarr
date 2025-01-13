using System.Net;
using FluentValidation;
using UmlautAdaptarr.Options.ArrOptions.InstanceOptions;

namespace UmlautAdaptarr.Validator;

public class GlobalInstanceOptionsValidator : AbstractValidator<GlobalInstanceOptions>
{
    private readonly static HttpClient httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(3)
    };

    public GlobalInstanceOptionsValidator()
    {
        RuleFor(x => x.Enabled).NotNull();

        When(x => x.Enabled, () =>
        {
            RuleFor(x => x.Host)
                .NotEmpty().WithMessage("Host is required when Enabled is true.")
                .Must(BeAValidUrl).WithMessage("Host/Url must start with http:// or https:// and be a valid address.");

            RuleFor(x => x.ApiKey)
                .NotEmpty().WithMessage("ApiKey is required when Enabled is true.");

            RuleFor(x => x)
                .MustAsync(BeReachable)
                .WithMessage("Host/Url is not reachable. Please check your Host or your UmlautAdaptrr Settings");
        });
    }

    private bool BeAValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
               && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }

    private static async Task<bool> BeReachable(GlobalInstanceOptions opts, CancellationToken cancellationToken)
    {
        var endTime = DateTime.Now.AddMinutes(3);
        var reachable = false;
        var url = $"{opts.Host}/api?apikey={opts.ApiKey}";

        while (DateTime.Now < endTime)
        {
            try
            {
                using var response = await httpClient.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    reachable = true;
                    break;
                }
                else
                {
                    Console.WriteLine($"Reachable check got unexpected status code {response.StatusCode}.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            // Wait for 15 seconds for next try
            Console.WriteLine($"The URL \"{opts.Host}/api?apikey=[REDACTED]\" is not reachable. Next attempt in 15 seconds...");
            Thread.Sleep(15000);
        }

        return reachable;
    }
}