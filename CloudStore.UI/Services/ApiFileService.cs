﻿using CloudStore.BL.Models;
using CloudStore.UI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CloudStore.UI.Services;

public class ApiFileService
{
    private readonly HttpClient _httpClient;
    private readonly WebClient _webClient;
    private readonly User _user;

    public ApiFileService(User user)
    {
        _user = user;
        _httpClient = new HttpClient();
        _webClient = new WebClient();
    }

    public async Task<List<CloudStoreUiListItem>?> GetStartingScreenItems()
    {
        var res = new List<CloudStoreUiListItem>();
        List<FileForList>? files;
        List<DirectoryForList>? directorys;
        //Fix me "api-key" scheme is not supported
        var rawFiles = await _httpClient.GetFromJsonAsync<IEnumerable<FileModel>>("https://localhost:7157/cloud-store-api/File/api-key:" + _user.ApiKey + "/all-files-from-directory");

        if (rawFiles == null)
            files = null;
        else
            files = rawFiles.Select(f => new FileForList(f)).ToList();

        var rawDirectorys = await _httpClient.GetFromJsonAsync<IEnumerable<string>>($"https://localhost:7157/cloud-store-api/File/api-key:{_user.ApiKey}/scan-directory");

        if (rawDirectorys == null)
            directorys = null;
        else
            directorys = rawDirectorys.Select(d => new DirectoryForList(d)).ToList();

        res.AddRange(directorys);
        res.AddRange(files);

        return res;
    }

    public async Task<List<CloudStoreUiListItem>?> GetItemsFromDirectory(string directory)
    {
        directory = string.Join("|", directory.Split("\\"));
        var res = new List<CloudStoreUiListItem>();
        IEnumerable<FileForList> files;
        IEnumerable<DirectoryForList> directorys;
        try
        {
            var rawFiles = await _httpClient.GetFromJsonAsync<IEnumerable<FileModel>>("https://localhost:7157/cloud-store-api/File/api-key:" + _user.ApiKey + $"/all-files-from-directory/{directory}");

            if (rawFiles == null)
                files = [];
            else
                files = rawFiles.Select(f => new FileForList(f));
        }
        catch (Exception)
        {
            files = [];
        }
        try
        {
            var rawDirectorys = await _httpClient.GetFromJsonAsync<IEnumerable<string>>("https://localhost:7157/cloud-store-api/File/api-key:" + _user.ApiKey + $"/scan-directory/{directory}");

            if (rawDirectorys == null)
                directorys = [];
            else
                directorys = rawDirectorys.Select(d => new DirectoryForList(d));
        }
        catch (Exception)
        {
            directorys = [];
        }

        res.AddRange(directorys);
        res.AddRange(files);

        return res;
    }

    public async Task<CloudStoreUiListItem?> UploadFile(string filePath, string directory = "")
    {
        using var multipartFormContent = new MultipartFormDataContent();
        var url = "https://localhost:7157/cloud-store-api/File/api-key:" + _user.ApiKey;
        if (directory is not null)
            url += $@"/upload-file/{string.Join("|", directory.Split("\\"))}";
        else
            url += "/upload-file";

        var fileName = filePath.Split(@"\")[^1];
        var extension = fileName.Split(".")[^1];

        var fileStreamContent = new StreamContent(File.OpenRead(filePath));
        fileStreamContent.Headers.ContentType = new MediaTypeHeaderValue($"application/{extension}");
        multipartFormContent.Add(fileStreamContent, name: "uploadedFile", fileName: fileName);
        var resposne = await _httpClient.PostAsync(url, multipartFormContent);

        return resposne.StatusCode switch
        {
            HttpStatusCode.BadRequest => null,
            HttpStatusCode.Unauthorized => throw new Exception("Unauthorized"),
            HttpStatusCode.OK => new FileForList(await resposne.Content.ReadFromJsonAsync<FileModel?>()),
            _ => null,
        };
    }

    public async Task<bool> DownloadFile(FileModel file, string path)
    {
        var response = await _httpClient
            .GetAsync("https://localhost:7157/cloud-store-api/File/api-key:" + _user.ApiKey + "/download/" + file.Id);
        switch (response.StatusCode)
        {
            case HttpStatusCode.OK:

                _webClient.DownloadFile(
                                    new Uri("https://localhost:7157/cloud-store-api/File/api-key:" + _user.ApiKey + "/download/" + file.Id),
                                    Path.Combine(path, file.Name));
                return true;

            case HttpStatusCode.Unauthorized:
                throw new Exception("Unauthorized");
            case HttpStatusCode.BadRequest:
                return false;

            default:
                return false;
        }
    }

    public async Task<bool> DeleteFile(FileModel file)
    {
        var response = await _httpClient.DeleteAsync("https://localhost:7157/cloud-store-api/File/api-key:" + _user.ApiKey + "/" + file.Id);

        return response.StatusCode switch
        {
            HttpStatusCode.BadRequest => false,
            HttpStatusCode.Unauthorized => throw new Exception("Unauthorized"),
            HttpStatusCode.OK => true,
            _ => false,
        };
    }

    public async Task<CloudStoreUiListItem?> ChangeFile(FileModel file)
    {
        return null;
    }

    public async Task<DirectoryForList?> MakeDirectory(string directory)
    {
        //https://localhost:7157/cloud-store-api/File/api-key:90546392470C5E893709E707FB70F88E/new-directory/testDir%2FDir
        var url = "https://localhost:7157/cloud-store-api/File/api-key:" + _user.ApiKey + "/new-directory";
        var jsonContent = JsonContent.Create(directory);
        var response = await _httpClient.PostAsync(url, jsonContent);

        if (response.StatusCode == HttpStatusCode.OK)
            return new DirectoryForList(directory.Split("\\")[^1]);
        else
            return null;
    }
}