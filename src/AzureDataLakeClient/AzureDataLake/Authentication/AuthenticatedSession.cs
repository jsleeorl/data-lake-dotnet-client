
using System;
using System.Linq;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using REST = Microsoft.Rest.Azure;

namespace AzureDataLakeClient.Authentication
{
    public class AuthenticatedSession
    {
        public Microsoft.Rest.ServiceClientCredentials Credentials;
        public string Tenant;
        public TokenCacheItem Token;
        public string Name;

        public AuthenticatedSession(string name, string tenant)
        {
            if (name == null)
            {
                throw new System.ArgumentNullException(nameof(name));
            }

            if (name.Length < 1)
            {
                throw new System.ArgumentException(nameof(name));
            }

            this.Tenant = tenant;

            this.Name = name;
        }

        public void Clear()
        {
            string cache_filename = GetTokenCachePath();


            if (System.IO.File.Exists(cache_filename))
            {
                var bytes = System.IO.File.ReadAllBytes(cache_filename);
                var token_cache = new Microsoft.IdentityModel.Clients.ActiveDirectory.TokenCache(bytes);
                token_cache.Clear();
                System.IO.File.WriteAllBytes(cache_filename, token_cache.Serialize());
            }
        }

        public void Authenticate()
        {

            string domain = this.Tenant; // if you want it to automatically use a tenant use "common" - but this can pick the an unintended tenant so it is best to be explicit
            string client_id = "1950a258-227b-4e31-a9cf-717495945fc2"; // Re-use the Azure PowerShell client id, in production code you should create your own client id

            var client_redirect = new System.Uri("urn:ietf:wg:oauth:2.0:oob");
            var AD_client_settings = REST.Authentication.ActiveDirectoryClientSettings.UseCacheCookiesOrPrompt(client_id, client_redirect);

            // Load the token cache, if one exists.
            string cache_filename = GetTokenCachePath();

            Microsoft.IdentityModel.Clients.ActiveDirectory.TokenCache token_cache;

            if (System.IO.File.Exists(cache_filename))
            {
                var bytes = System.IO.File.ReadAllBytes(cache_filename);
                token_cache = new Microsoft.IdentityModel.Clients.ActiveDirectory.TokenCache(bytes);
            }
            else
            {
                token_cache = new Microsoft.IdentityModel.Clients.ActiveDirectory.TokenCache();
            }

            // Now figure out the token business

            Microsoft.Rest.ServiceClientCredentials creds = null;

            // Get the cached token, if it exists and is not expired.
            //if (token_cache.Count > 0)
            //{
            //    var token_cache_item = token_cache.ReadItems().First();
            //    creds = REST.Authentication.UserTokenProvider.CreateCredentialsFromCache(client_id, token_cache_item.TenantId, token_cache_item.DisplayableId, token_cache).Result;
            //    SaveTokenCache(token_cache, cache_filename);
            //}

            //if (creds == null)
            {
                // Did not find the token in the cache, show popup and save the token
                var sync_context = new System.Threading.SynchronizationContext();
                System.Threading.SynchronizationContext.SetSynchronizationContext(sync_context);
                creds = REST.Authentication.UserTokenProvider.LoginWithPromptAsync(domain, AD_client_settings, token_cache).Result;
                if (token_cache.Count > 0)
                {
                    // If token cache has no items then trying serialize it will fail when deserializing
                    System.IO.File.WriteAllBytes(cache_filename, token_cache.Serialize());
                }

            }



            this.Credentials = creds;
            this.Token = token_cache.ReadItems().First();

        }

        private static void SaveTokenCache(TokenCache token_cache, string filename)
        {
            var bytes = token_cache.Serialize();
            System.IO.File.WriteAllBytes(filename, bytes);
        }

        private string GetTokenCachePath()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string basefname = "TokenCache_" + this.Name + ".tc";
            var tokenCachePath = System.IO.Path.Combine(path, basefname);
            return tokenCachePath;
        }
    }

}