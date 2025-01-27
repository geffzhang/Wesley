﻿using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Wesley.Client.Services
{
    /// <summary>
    /// 表示API请求返回结果
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    public class APIResult<TResult>
    {
        /// <summary>
        /// 状态码
        /// </summary>
        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("Return")]
        public int Return { get; set; } = 0;

        private TResult _data;
        /// <summary>
        /// 数据
        /// </summary>
        [JsonProperty("data")]
        public TResult Data
        {
            get => _data ??= default;
            set => _data = value;
        }

        [JsonProperty("success")]
        public bool Success { get; set; } = false;


        [JsonProperty("message")]
        public string Message { get; set; }


        [JsonProperty("rows")]
        public int Rows { get; set; }


        [JsonProperty("pages")]
        public int Pages { get; set; }

    }
    public class ResultData
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int Code { get; set; }
        public int Return { get; set; }
    }

    public class HttpClientHelper
    {
        private static readonly HttpClient _httpClient;
        static HttpClientHelper()
        {
            _httpClient = new HttpClient() { BaseAddress = new Uri(GlobalSettings.FileCenterEndpoint) };
            _httpClient.DefaultRequestHeaders.Connection.Add("keep-alive");
            try
            {
                _httpClient.SendAsync(new HttpRequestMessage
                {
                    Method = new HttpMethod("HEAD"),
                    RequestUri = new Uri(GlobalSettings.FileCenterEndpoint + "/")
                });//.Result.EnsureSuccessStatusCode();
            }
            catch (System.Net.Http.HttpRequestException)
            {
                //Crashes.TrackError(ex);
            }
            catch (Exception ex)
            {
                //Crashes.TrackError(ex);
            }
        }

        public async Task<string> PostAsync(string url, MultipartFormDataContent content)
        {
            try
            {
                var response = await _httpClient.PostAsync(url, content);
                return await response.Content.ReadAsStringAsync();
            }
            catch (System.Net.Http.HttpRequestException)
            {
                return string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }


}
