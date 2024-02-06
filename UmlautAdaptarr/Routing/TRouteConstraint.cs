namespace UmlautAdaptarr.Routing
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;

    public class TRouteConstraint : IRouteConstraint
    {
        private readonly string _methodName;

        public TRouteConstraint(string methodName)
        {
            _methodName = methodName;
        }

        public bool Match(HttpContext httpContext, IRouter route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
        {
            var t = httpContext.Request.Query["t"].ToString();

            return t == _methodName;
        }
    }

}
