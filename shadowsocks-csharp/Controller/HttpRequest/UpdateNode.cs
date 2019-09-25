﻿using Shadowsocks.Model;
using System;
using System.Net;

namespace Shadowsocks.Controller.HttpRequest
{
    public class UpdateNode : HttpRequest
    {
        public const string DefaultUpdateUrl = @"https://raw.githubusercontent.com/HMBSbige/Text_Translation/master/ShadowsocksR/freenodeplain.txt";

        public event EventHandler NewFreeNodeFound;
        public string FreeNodeResult;
        public ServerSubscribe SubscribeTask;
        public bool Notify;

        public void CheckUpdate(Configuration config, ServerSubscribe subscribeTask, bool notify)
        {
            FreeNodeResult = null;
            Notify = notify;
            try
            {
                var proxy = UpdateChecker.CreateProxy(config);
                SubscribeTask = subscribeTask;
                var url = subscribeTask.Url ?? DefaultUpdateUrl;
                Update(proxy, url, config.proxyUserAgent);
            }
            catch (Exception e)
            {
                Logging.LogUsefulException(e);
            }
        }

        private async void Update(IWebProxy proxy, string url, string userAgent)
        {
            try
            {
                FreeNodeResult = await Get(url, proxy, userAgent);
            }
            catch
            {
                try
                {
                    FreeNodeResult = await Get(url, null, userAgent);
                }
                catch (Exception ex)
                {
                    Logging.Debug(ex.ToString());
                }
            }

            NewFreeNodeFound?.Invoke(this, new EventArgs());
        }
    }
}
