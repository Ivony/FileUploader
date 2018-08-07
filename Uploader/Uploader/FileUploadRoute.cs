using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Uploader
{
  public class FileUploadRoute : IRouter
  {
    public MvcRouteHandler Handler { get; }
    public string VirtualPath { get; }
    public string Controller { get; }

    public FileUploadRoute( MvcRouteHandler handler, string virtualPath, string controller )
    {
      Handler = handler ?? throw new ArgumentNullException( nameof( handler ) );
      VirtualPath = virtualPath ?? throw new ArgumentNullException( nameof( virtualPath ) );
      Controller = controller ?? throw new ArgumentNullException( nameof( controller ) );
    }

    public async Task RouteAsync( RouteContext context )
    {

      var path = context.HttpContext.Request.Path;

      if ( path.StartsWithSegments( VirtualPath ) == false )
        return;


      var sections = path.Value.Substring( VirtualPath.Length ).Split( '/', StringSplitOptions.RemoveEmptyEntries );
      if ( sections.Length > 2 )
        return;




      if ( sections.Length == 0 )
        context.RouteData.Values["action"] = "Create";

      else if ( sections.Length == 1 )
      {
        context.RouteData.Values["token"] = sections[0];

        if ( string.Equals( context.HttpContext.Request.Method, "GET", StringComparison.OrdinalIgnoreCase ) )
          context.RouteData.Values["action"] = "Info";

        else
          return;
      }

      else if ( sections.Length == 2 )
      {
        if ( string.Equals( context.HttpContext.Request.Method, "POST", StringComparison.OrdinalIgnoreCase ) )
        {
          context.RouteData.Values["token"] = sections[0];
          context.RouteData.Values["action"] = "Upload";
          context.RouteData.Values["blockIndex"] = sections[1];
        }

        else
          return;
      }

      else
        return;

      context.RouteData.Values["controller"] = Controller;
      await Handler.RouteAsync( context );
    }

    public VirtualPathData GetVirtualPath( VirtualPathContext context )
    {
      return null;
    }


  }



  public static class RouteExtensions
  {

    public static IApplicationBuilder UseUploader( this IApplicationBuilder app, string virtualPath )
    {

      app.UseRouter( new FileUploadRoute( app.ApplicationServices.GetService<MvcRouteHandler>(), virtualPath, "FileUpload" ) );
      return app;

    }
  }
}
