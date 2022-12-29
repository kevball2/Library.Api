using Microsoft.AspNetCore.Mvc.Formatters;
using System.Net.Mime;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Text;

// We are creating a html extension that inherits the IResult Interface
// to allow for HTML to be properly rendered on a Result return. 

namespace Library.Api.Extensions
{
   public static class ResultExtensions
    {
        public static IResult html(this IResultExtensions extensions, string html) 
        {
            return new HtmlResult(html);
        }

        private class HtmlResult : IResult
        {
            private string _html;

            public HtmlResult(string html)
            {
                _html = html;
            }
            public Task ExecuteAsync(HttpContext httpContext)
            {
                httpContext.Response.ContentType = MediaTypeNames.Text.Html;
                httpContext.Response.ContentLength = Encoding.UTF8.GetByteCount(_html);
                return httpContext.Response.WriteAsync(_html);

            }
        }
    }

    
}
