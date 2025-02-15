﻿using CloudStore.BL;
using CloudStore.BL.Models;
using CloudStore.DAL;
using CloudStore.WebApi.apiKeyValidation;
using Microsoft.AspNetCore.Mvc;
using System.Reflection.Metadata.Ecma335;

namespace CloudStore.Controllers;

[Route("cloud-store-api/[controller]")] //personal-key:{apiKey}/
[ApiController]
public class FileController : ControllerBase
{
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly CSFilesDbHelper _dbHelper;
    private readonly CSUsersDbHelper _dbUserHelper;
    private readonly string _userDirectory;
    private readonly IApiKeyValidation _apiKeyValidation;

    public FileController(IWebHostEnvironment webHostEnvironment, CSFilesDbHelper dbHelper,
                         CSUsersDbHelper dbUserHelper, IApiKeyValidation apiKeyValidation)
    {
        _webHostEnvironment = webHostEnvironment;
        _dbHelper = dbHelper;
        _dbUserHelper = dbUserHelper;
        _apiKeyValidation = apiKeyValidation;
        _userDirectory = _webHostEnvironment.ContentRootPath + "\\Files";
    }

    [HttpGet("api-key:{apiKey}/download/{id}")]
    public async Task<IActionResult> DownloadAsync(int id, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return BadRequest();

        bool isValid = _apiKeyValidation.IsValidApiKey(apiKey);
        if (!isValid)
            return Unauthorized();

        var user = _dbUserHelper.GetUserByApiKey(apiKey);

        var path = Path.Combine(_userDirectory, user.UserDirectory);
        var fileToDownload = await _dbHelper.GetFileByIdAsync(id, user);

        if (fileToDownload == null)
            return NotFound();

        string filePath = Path.Combine(path, $"{fileToDownload.Path}");
        string file_type = $"application/{fileToDownload.Extension}";
        string file_name = $"{fileToDownload.Name}";

        return PhysicalFile(filePath, file_type, file_name);
    }

    [HttpGet("api-key:{apiKey}/{id}")]
    public async Task<IActionResult> GetFileAsync(int id, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return BadRequest();

        bool isValid = _apiKeyValidation.IsValidApiKey(apiKey);
        if (!isValid)
            return Unauthorized();

        var user = _dbUserHelper.GetUserByApiKey(apiKey);

        return Ok(await _dbHelper.GetFileByIdAsync(id, user));
    }

    [HttpGet("api-key:{apiKey}/AllFiles")]
    public async Task<IActionResult> GetAllFilesAsync(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return BadRequest();

        bool isValid = _apiKeyValidation.IsValidApiKey(apiKey);
        if (!isValid)
            return Unauthorized();

        var user = _dbUserHelper.GetUserByApiKey(apiKey);
        if (user is null)
            throw new Exception("Not authorized!"); ;

        return Ok(await _dbHelper.GetAllFilesAsync(user));
    }

    [HttpPost("api-key:{apiKey}")]
    public async Task<IActionResult> Post(string apiKey, [FromBody] FileModel file)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return BadRequest();

        bool isValid = _apiKeyValidation.IsValidApiKey(apiKey);
        if (!isValid)
            return Unauthorized();

        await _dbHelper.AddFileAsync(file);

        return Ok();
    }

    [HttpPut("api-key:{apiKey}/update-file")]
    public async Task<IActionResult> Put(string apiKey, [FromBody] FileModel file)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return BadRequest();

        bool isValid = _apiKeyValidation.IsValidApiKey(apiKey);
        if (!isValid)
            return Unauthorized();

        await _dbHelper.UpdateFile(file);
        return Ok();
    }

    [HttpDelete("api-key:{apiKey}/{id}")]
    public async Task<IActionResult> Delete(string apiKey, int id)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return BadRequest();

        bool isValid = _apiKeyValidation.IsValidApiKey(apiKey);
        if (!isValid)
            return Unauthorized();

        var user = _dbUserHelper.GetUserByApiKey(apiKey);

        await _dbHelper.DeleteFileByIdAsync(id, user);

        return Ok();
    }

    [HttpPost("api-key:{apiKey}/new-directory")]
    public IActionResult CreateDirectory(string apiKey, [FromBody] string directory)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return BadRequest();

        bool isValid = _apiKeyValidation.IsValidApiKey(apiKey);
        if (!isValid)
            return Unauthorized();

        var user = _dbUserHelper.GetUserByApiKey(apiKey);

        var path = Path.Combine(_userDirectory, user.UserDirectory);

        var newDirectory = Path.Combine(path, directory);

        if (Directory.Exists(newDirectory))
            return Ok("Directory is already exist");

        try
        {
            Directory.CreateDirectory(newDirectory);
        }
        catch (IOException ex)
        {
            return BadRequest(ex.Message);
        }
        return Ok("new Directory was created");
    }

    [HttpPost("api-key:{apiKey}/upload-file")]
    public async Task<IActionResult> UploadFileAsync(string apiKey, IFormFile uploadedFile, string? directory = "")
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return BadRequest();

        bool isValid = _apiKeyValidation.IsValidApiKey(apiKey);
        if (!isValid)
            return Unauthorized();

        var user = _dbUserHelper.GetUserByApiKey(apiKey);

        if (uploadedFile is null)
            return BadRequest();
        if (!Directory.Exists(Path.Combine(_userDirectory + directory)))
            return NotFound("Directory is not exist");

        string path = Path.Combine(_userDirectory, user.UserDirectory);

        if (string.IsNullOrEmpty(directory))
            path += "\\" + uploadedFile.FileName;
        else
            path += "\\" + directory + "\\" + uploadedFile.FileName;

        using var fs = new FileStream(path, FileMode.Create);
        await uploadedFile.CopyToAsync(fs);

        var temp = uploadedFile.FileName.Split('.');

        var newFile = new FileModel
        {
            Name = uploadedFile.FileName,
            Path = directory + "\\" + uploadedFile.FileName,
            Extension = temp[^1],
            UserId = user.Id
        };
        await _dbHelper.AddFileAsync(newFile);

        return Ok("New file is added");
    }
}