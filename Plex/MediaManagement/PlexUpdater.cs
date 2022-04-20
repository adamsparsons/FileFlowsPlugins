﻿using FileFlows.Plex.Models;
using System.Text.RegularExpressions;

namespace FileFlows.Plex.MediaManagement;

public class PlexUpdater: Node
{
    public override int Inputs => 1;
    public override int Outputs => 2;
    public override FlowElementType Type => FlowElementType.Process; 
    public override string Icon => "fas fa-paper-plane";

    public override int Execute(NodeParameters args)
    {
        var settings = args.GetPluginSettings<PluginSettings>();

        if (string.IsNullOrWhiteSpace(settings?.AccessToken))
        {
            args.Logger?.WLog("No access token set");
            return 2;
        }
        if (string.IsNullOrWhiteSpace(settings?.ServerUrl))
        {
            args.Logger?.WLog("No server URL set");
            return 2;
        }

        // get the path
        string path = args.WorkingFile;
        path = args.UnMapPath(path);
        if (args.IsDirectory == false)
        {
            bool windows = path.StartsWith("\\") || Regex.IsMatch(path, @"^[a-zA-Z]:\\");
            string pathSeparator = windows ? "\\" : "/";
            path = path.Substring(0, path.LastIndexOf(pathSeparator));
        }

        string url = settings.ServerUrl;
        if (url.EndsWith("/") == false)
            url += "/";
        url += "library/sections";

        using var httpClient = new HttpClient();

        var sectionsResponse= GetWebRequest(httpClient, url + "?X-Plex-Token=" + settings.AccessToken);
        if (sectionsResponse.success == false)
        {
            args.Logger?.WLog("Failed to retrieve sections" + (string.IsNullOrWhiteSpace(sectionsResponse.body) ? "" : ": " + sectionsResponse.body));
            return 2;
        }

        PlexSections sections;
        try
        {
            var options = new System.Text.Json.JsonSerializerOptions();
            options.PropertyNameCaseInsensitive = true;
            sections = System.Text.Json.JsonSerializer.Deserialize<PlexSections>(sectionsResponse.body, options);
        }
        catch (Exception ex)
        {
            args.Logger?.ELog("Failed deserializing sections json: " + ex.Message);
            return 2;
        }
        string pathLower = path.ToLower();
        var section = sections?.MediaContainer?.Directory?.Where(x => {
            if (x.Location?.Any() != true)
                return false;
            foreach (var loc in x.Location) {
                if (loc.Path == null)
                    continue;
                if (pathLower.StartsWith(loc.Path.ToLower()))
                    return true;
            }
            return false;
        }).FirstOrDefault();
        if(section == null)
        {
            args.Logger?.WLog("Failed to find Plex section for path: " + path);
            return 2;
        }

        url += $"/{section.Key}/refresh?path={Uri.EscapeDataString(path)}&X-Plex-Token=" + settings.AccessToken;

        var updateResponse = GetWebRequest(httpClient, url);
        if (updateResponse.success == false)
        {
            if(string.IsNullOrWhiteSpace(updateResponse.body) == false)
                args.Logger?.WLog("Failed to update Plex:" + updateResponse.body);
            return 2;
        }
        return 1;
    }

    private Func<HttpClient, string, (bool success, string body)> _GetWebRequest;
    internal Func<HttpClient, string, (bool success, string body)> GetWebRequest
    {
        get
        {
            if(_GetWebRequest == null)
            {
                _GetWebRequest = (HttpClient client, string url) =>
                {
                    try
                    {
                        client.DefaultRequestHeaders.Add("Accept", "application/json");
                        var response = client.GetAsync(url).Result;
                        string body = response.Content.ReadAsStringAsync().Result;
                        return (response.IsSuccessStatusCode, body);
                    }
                    catch(Exception ex)
                    {
                        return (false, ex.Message);
                    }
                };
            }
            return _GetWebRequest;
        }
    }
}