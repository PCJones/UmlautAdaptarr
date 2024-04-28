using FluentValidation;
using System.Net;
using UmlautAdaptarr.Options.ArrOptions.InstanceOptions;

namespace UmlautAdaptarr.Validator
{
    public class GlobalInstanceOptionsValidator : AbstractValidator<GlobalInstanceOptions>
    {
        public GlobalInstanceOptionsValidator()
        {
            RuleFor(x => x.Enabled).NotNull();

            When(x => x.Enabled, () =>
            {

                RuleFor(x => x.Host)
                    .NotEmpty().WithMessage("Host is required when Enabled is true.")
                    .Must(BeAValidUrl).WithMessage("Host must start with http:// or https:// and be a valid address.")
                    .Must(BeReachable).WithMessage("Host is not reachable. Please check your Host or your UmlautAdaptrr Settings");

                RuleFor(x => x.ApiKey)
                    .NotEmpty().WithMessage("ApiKey is required when Enabled is true.");
            });
        }

        private bool BeAValidUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
                   && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        private bool BeReachable(string url)
        {
            try
            {
                var request = WebRequest.Create(url);
                var response = (HttpWebResponse)request.GetResponse();
                return response.StatusCode == HttpStatusCode.OK;
            }
            catch
            {
                return false;
            }
        }
    }
}
