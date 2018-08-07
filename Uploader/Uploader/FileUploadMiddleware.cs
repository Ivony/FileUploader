using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace Uploader
{
  public class FileUploadMiddleware : IMiddleware
  {


    public FileUploadMiddleware( string virtualPath )
    {
      if ( string.IsNullOrEmpty( virtualPath ) )
        throw new ArgumentException( "message", nameof( virtualPath ) );

      VirtualPath = new PathString( virtualPath );




    }

    public PathString VirtualPath { get; }

    public async Task InvokeAsync( HttpContext context, RequestDelegate next )
    {
      var path = context.Request.Path;

      if ( path.StartsWithSegments( VirtualPath ) == false )
      {
        await next( context );
        return;
      }


      var sections = path.Value.Substring( VirtualPath.Value.Length ).Split( '/' );
      if ( sections.Length > 2 )
      {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync( "resource not found" );
      }


      var token = sections.FirstOrDefault();
      var action = sections.Skip( 1 ).FirstOrDefault() ?? "Info";

      if ( token == null )
      {



      }
    }
  }
}
