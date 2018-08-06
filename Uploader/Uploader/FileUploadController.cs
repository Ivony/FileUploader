using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Features;

namespace BigFileUploader
{
  public class FileUploadController : ControllerBase
  {

    public string WebRootPath { get; }

    public FileUploadController(IHostingEnvironment hostingEnvironment)
    {
      WebRootPath = hostingEnvironment.WebRootPath;
    }



    public async Task<object> Create(long fileSize, long blockSize = 1024 * 64)
    {

      var token = Path.GetRandomFileName();
      var meta = UploadMetaData.Create(fileSize, blockSize);


      JObject.FromObject(new
      {
        fileSize,
        blockSize,
        blocks = new bool[fileSize / blockSize + (fileSize % blockSize == 0 ? 0 : 1)]
      });


      var filepath = GetTempPath(token);
      Directory.CreateDirectory(Path.GetDirectoryName(filepath));

      System.IO.File.WriteAllBytes(filepath, new byte[0]);
      await SaveMeta(token, meta);

      return new
      {
        token
      };
    }

    private async Task SaveMeta(string token, UploadMetaData meta)
    {
      var filepath = GetTokenPath(token);
      Directory.CreateDirectory(Path.GetDirectoryName(filepath));
      await System.IO.File.WriteAllTextAsync(filepath, meta.ToString());
    }

    private async Task<UploadMetaData> LoadMeta(string token)
    {
      var filepath = GetTokenPath(token);

      if (System.IO.File.Exists(filepath) == false)
        return null;

      using (var stream = System.IO.File.OpenRead(filepath))
      {
        return UploadMetaData.FromJson(await new StreamReader(stream).ReadToEndAsync());
      }
    }



    public async Task<object> Upload(string token, int blockIndex)
    {
      var meta = await LoadMeta(token);
      if (meta == null)
        return NotFound();


      if (blockIndex < 0 || blockIndex >= meta.Blocks.Length)
        return BadRequest("block index is out of range.");



      var expectSize = blockIndex < meta.Blocks.Length - 1 ? meta.BlockSize : meta.FileSize - blockIndex * meta.BlockSize;

      var contentStream = await ReadContent();

      if (contentStream.Length != expectSize)
        return BadRequest("content size is invalid.");



      var filepath = GetTempPath(token);

      using (var stream = System.IO.File.Open(filepath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
      {
        stream.Seek(blockIndex * meta.BlockSize, SeekOrigin.Begin);
        await contentStream.CopyToAsync(stream);
      }

      meta.Blocks[blockIndex] = true;
      await SaveMeta(token, meta);

      return await Task(token);
    }

    private async Task<Stream> ReadContent()
    {
      if (Request.HasFormContentType)
      {
        var form = await Request.ReadFormAsync();
        return form.Files.FirstOrDefault().OpenReadStream();
      }

      return Request.Body;
    }

    [HttpGet]
    public async Task<object> Task(string token, string filename = null)
    {
      var meta = await LoadMeta(token);
      int? incompleteBlock = 0;

      foreach (bool item in meta.Blocks)
      {
        if (item == false)
          break;
        else
          incompleteBlock++;
      }

      if (incompleteBlock >= meta.Blocks.Length)
      {
        if (filename != null)
          return await Complete(token, filename);

        else
          return new
          {
            meta.FileSize,
            meta.BlockSize,
            TotalBlocks = meta.Blocks.Length,
            meta.IncompleteBlocks,
            meta.Progress,
            Token = token
          };
      }


      if (filename != null && incompleteBlock == null)
        return await Complete(token, filename);



      var start = incompleteBlock * meta.BlockSize;
      var end = (incompleteBlock + 1) * meta.BlockSize;
      if (end > meta.FileSize)
        end = meta.FileSize;

      return new
      {
        meta.FileSize,
        meta.BlockSize,
        TotalBlocks = meta.Blocks.Length,
        meta.IncompleteBlocks,
        meta.Progress,
        Token = token,

        incomplete = incompleteBlock == null ? null : new
        {
          blockIndex = incompleteBlock,
          start,
          end
        }
      };
    }

    private async Task<object> Complete(string token, string filename)
    {

      filename = Path.GetFileName(filename);

      var meta = await LoadMeta(token);
      if (meta.Blocks.All(item => item) == false)
        throw new InvalidOperationException();

      var sourcePath = GetTempPath(token);
      var filepath = GetDestinationPath(token, filename);
      Directory.CreateDirectory(Path.GetDirectoryName(filepath));

      System.IO.File.Move(sourcePath, filepath);
      System.IO.File.Delete(GetTokenPath(token));

      return new
      {
        url = Url.Content($"~/files/{token}/{filename}")
      };

    }

    private string GetDestinationPath(string token, string filename)
    {
      return Path.Combine(WebRootPath, "files", token, filename);
    }

    private string GetTokenPath(string token)
    {
      return Path.Combine(WebRootPath, "tokens", token + ".json");
    }

    private string GetTempPath(string token)
    {
      return Path.Combine(WebRootPath, "temp", token);
    }

    private class UploadMetaData
    {


      public long FileSize { get; private set; }

      public long BlockSize { get; private set; }

      public bool[] Blocks { get; private set; }


      private int? _completed;
      public int CompletedBlocks { get { return _completed ?? (int)(_completed = Blocks.Count(item => item)); } }
      public int IncompleteBlocks { get { return Blocks.Length - CompletedBlocks; } }
      public decimal Progress { get { return CompletedBlocks / (decimal)Blocks.Length; } }

      private UploadMetaData() { }




      public static UploadMetaData Create(long fileSize, long blockSize)
      {
        var instance = new UploadMetaData();

        instance.FileSize = fileSize;
        instance.BlockSize = blockSize;

        var maxBlocks = fileSize / blockSize + (fileSize % blockSize == 0 ? 0 : 1);
        instance.Blocks = new bool[maxBlocks];
        return instance;

      }


      public static UploadMetaData FromJson(string json)
      {
        var data = JObject.Parse(json);

        var instance = new UploadMetaData();
        instance.FileSize = (long)data["FileSize"];
        instance.BlockSize = (long)data["BlockSize"];

        instance.Blocks = ((JArray)data["Blocks"]).ToObject<bool[]>();

        return instance;
      }

      public override string ToString()
      {
        return JObject.FromObject(this).ToString();
      }
    }
  }
}