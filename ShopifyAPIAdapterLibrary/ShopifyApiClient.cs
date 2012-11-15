﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Threading.Tasks;
using System.Net.Http;
using ShopifyAPIAdapterLibrary.Models;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;

namespace ShopifyAPIAdapterLibrary
{
    /// <summary>
    /// This class is used to make Shopify API calls 
    /// </summary>
    /// <remarks>
    /// You will first need to use the ShopifyAPIAuthorizer to obtain the required authorization.
    /// </remarks>
    /// <seealso cref="http://api.shopify.com/"/>
    public class ShopifyAPIClient : ShopifyAPIAdapterLibrary.IShopifyAPIClient
    {
        public RestResource<Product> Products { get; private set; }

        /// <summary>
        /// Programmatically-accessible mapping of IResourceModels to
        /// IRestResources to service requests for them.
        /// </summary>
        private IDictionary<Type, IUntypedResource> Resources { get; set; }

        /// <summary>
        /// Creates an instance of this class for use with making API Calls
        /// </summary>
        /// <param name="state">the authorization state required to make the API Calls</param>
        public ShopifyAPIClient(ShopifyAuthorizationState state)
        {
            this.State = state;
            SetUpResources();
        }

        /// <summary>
        /// Creates an instance of this class for use with making API Calls
        /// </summary>
        /// <param name="state">the authorization state required to make the API Calls</param>
        /// <param name="translator">the translator used to transform the data between your C# client code and the Shopify API</param>
        public ShopifyAPIClient(ShopifyAuthorizationState state, IDataTranslator translator)
        {
            this.State = state;
            this.Translator = translator;
            SetUpResources();
        }

        public void RegisterResource<T>(IUntypedResource resource) where T : IResourceModel
        {
            this.Resources.Add(typeof(T), resource);
        }

        public RestResource<T> GetResource<T>() where T: IResourceModel
        {
            return (RestResource<T>)this.Resources[typeof(T)];
        }

        private void SetUpResources() {
            this.Resources = new Dictionary<Type, IUntypedResource>();
            Products = new RestResource<Product>(this, "product");
        }

        private void EnsureTranslator() {
            if(Translator == null) {
                throw new ShopifyConfigurationException("In order to use object types with ShopifyApiClient, an IDataTranslator must be provided.");
            }
        }

        /// <summary>
        /// Make an HTTP Request to the Shopify API
        /// </summary>
        /// <param name="method">method to be used in the request</param>
        /// <param name="path">the path that should be requested</param>
        /// <seealso cref="http://api.shopify.com/"/>
        /// <returns>the server response</returns>
        public async Task<object> Call(HttpMethod method, string path, NameValueCollection parameters = null, object data = null)
        {
            // cute bit of dynamic feature.
            String reqBody;
            if(data is String) {
                reqBody = (String)data;
            } else {
                EnsureTranslator();
                reqBody = Translator.Encode(data);
            }
            var result = await CallRaw(method, GetRequestContentType(), path, parameters, reqBody);
            if (Translator != null) {
                return Translator.Decode(result);
            } else {
                return result;
            }
        }

        public ShopifyException HandleError(HttpResponseMessage response, string reason)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new NotFoundException(reason, response);
            } else if ((int)response.StatusCode == 422)
            {
                return new InvalidContentException(reason, response);
            } else
            {
                return new ShopifyHttpException(reason, response);
            }
        }

        public async Task<string> CallRaw(HttpMethod method, MediaTypeHeaderValue acceptType, string path, NameValueCollection parameters, string requestBody) {
            // put params into query string
            StringBuilder queryString = new StringBuilder();
            if(parameters != null) {
                foreach (string key in parameters.Keys)
                {
                    queryString.AppendFormat("{0}={1}", HttpUtility.UrlEncode(key), HttpUtility.UrlEncode(parameters[key]), requestBody);
                }
            }

            UriBuilder url = ShopUri();
            url.Path = path;
            
            var http = new HttpClient();
            
            var request = new HttpRequestMessage(method, url.Uri);
            
            request.Headers.Add("X-Shopify-Access-Token", this.State.AccessToken);
            request.Headers.Add("Accept", acceptType.ToString());
            request.Method = method;

            url.Query = queryString.ToString();

            if (method == HttpMethod.Post || method == HttpMethod.Put)
            {
                var postContent = new StringContent(requestBody);
                postContent.Headers.ContentType = GetRequestContentType();

                request.Content = postContent;
            }
         
            
            var response = await http.SendAsync(request);
            
            var result = await response.Content.ReadAsStringAsync();
            
            if(response.IsSuccessStatusCode) {
                return result;
                
            } else {
                throw HandleError(response, result);
            }
        }

        /// <summary>
        /// Make a Get method HTTP request to the Shopify API
        /// </summary>
        /// <param name="path">the path where the API call will be made.</param>
        /// <seealso cref="http://api.shopify.com/"/>
        /// <returns>the server response</returns>
        public Task<object> Get(string path)
        {
            return Get(path, null);
        }

        /// <summary>
        /// Make a Get method HTTP request to the Shopify API
        /// </summary>
        /// <param name="path">the path where the API call will be made.</param>
        /// <param name="callParams">the querystring params</param>
        /// <seealso cref="http://api.shopify.com/"/>
        /// <returns>the server response</returns>
        public Task<object> Get(string path, NameValueCollection callParams)
        {
            return Call(HttpMethod.Get, path, callParams);
        }

        /// <summary>
        /// Make a Post method HTTP request to the Shopify API
        /// </summary>
        /// <param name="path">the path where the API call will be made.</param>
        /// <param name="data">the data that this path will be expecting</param>
        /// <seealso cref="http://api.shopify.com/"/>
        /// <returns>the server response</returns>
        public Task<object> Post(string path, object data)
        {
            return Call(HttpMethod.Post, path, parameters: null, data: data);
        }

        /// <summary>
        /// Make a Put method HTTP request to the Shopify API
        /// </summary>
        /// <param name="path">the path where the API call will be made.</param>
        /// <param name="data">the data that this path will be expecting</param>
        /// <seealso cref="http://api.shopify.com/"/>
        /// <returns>the server response</returns>
        public Task<object> Put(string path, object data)
        {
            return Call(HttpMethod.Put, path, parameters: null, data: data);
        }

        /// <summary>
        /// Make a Delete method HTTP request to the Shopify API
        /// </summary>
        /// <param name="path">the path where the API call will be made.</param>
        /// <seealso cref="http://api.shopify.com/"/>
        /// <returns>the server response</returns>
        public Task<object> Delete(string path)
        {
            return Call(HttpMethod.Delete, path);
        }

        /// <summary>
        /// Get the content type that should be used for HTTP Requests
        /// </summary>
        public MediaTypeHeaderValue GetRequestContentType()
        {
            if (Translator == null)
                return new MediaTypeHeaderValue(DefaultContentType);
            return Translator.GetContentType();
        }

        /// <summary>
        /// Pluralize (using standard English rules) a singular noun.  Meant
        /// for C locale/invariant programmer-domain use.
        /// </summary>
        /// <param name="input"></param>
        /// <returns>The pluralized form.  Note that it always returns
        /// in lower case.</returns>
        public static string Pluralize(string input) {
            // lol english
            var lowerInput = input.ToLowerInvariant();
            if(lowerInput.EndsWith("h")) {
                return lowerInput + "es";
            } else if(lowerInput.EndsWith("y")) {
                return lowerInput.Substring(0, lowerInput.Length - 1) + "ies";
            } else if(lowerInput.EndsWith("is")) {
                return lowerInput.Substring(0, lowerInput.Length - 2) + "es";
            } else if (lowerInput.EndsWith("us") && lowerInput != "virus") {
                return lowerInput.Substring(0, lowerInput.Length - 2) + "i";
            } else if (lowerInput.EndsWith("s")) {
                return lowerInput + "es";
            } else if(lowerInput.EndsWith("ium")) {
                return lowerInput.Substring(0, lowerInput.Length - 2) + "a";
            } else {
                return lowerInput + "s";
            }
        }

        public static string UriPathJoin(String basePath, String relativePath) {
            if(basePath == null || basePath.Length == 0) {
                return String.Format("/{0}", relativePath); 
            } else if (basePath.EndsWith("/")) {
                return String.Format("{0}{1}", basePath, relativePath);
            } else {
                return String.Format("{0}/{1}", basePath, relativePath);
            }
        }

        public UriBuilder ShopUri()
        {
            return new UriBuilder(String.Format("http://{0}.myshopify.com/", ShopName));
        }

        public string AdminPath()
        {
            return "/admin";
        }

        public string ProductsPath() {
            return UriPathJoin(AdminPath(), "products");
        }

        public string ProductPath(string id)
        {
            return UriPathJoin(ProductsPath(), id);
        }

        [Obsolete]
        public async Task<ICollection<Product>> GetProducts() {

            var resourceString = await CallRaw(HttpMethod.Get, GetRequestContentType(), ProductsPath(), parameters: null, requestBody: null);
            Console.WriteLine(resourceString);

            return TranslateObject<List<Product>>("products", resourceString);
        }

        [Obsolete]
        public T TranslateObject<T>(String subfieldName, string content)
        {
            if (Translator == null)
            {
                throw new NotSupportedException("ShopfiyApiClient needs a data translator (JSON, XML) before the type safe API can be used.");
            }
            return Translator.ResourceDecode<T>(subfieldName, content);
        }

        [Obsolete]
        public string ObjectTranslate<T>(String subfieldName, T model)
        {
            if (Translator == null)
            {
                throw new NotSupportedException("ShopfiyApiClient needs a data translator (JSON, XML) before the type safe API can be used.");
            }
            return Translator.ResourceEncode<T>(subfieldName, model);
        }

        /// <summary>
        /// The default content type to POST/PUT content as on HTTP Requests to the Shopify API
        /// </summary>
        protected static readonly string DefaultContentType = "application/json";

        /// <summary>
        /// The state required to make API calls.  It contains the access token and
        /// the name of the shop that your app will make calls on behalf of
        /// </summary>
        public ShopifyAuthorizationState State { get; set; }

        /// <summary>
        /// Used to translate the data sent and recieved by the Shopify API
        /// </summary>
        /// <example>
        /// This could be used to translate from C# objects to XML or JSON.  Thus making your code
        /// that consumes this class much more clean
        /// </example>
        protected IDataTranslator Translator { get; set; }

        /// <summary>
        /// Gets the name (as in, domain name fragment) of the Shop this ApiClient is associated with.
        /// </summary>
        public string ShopName { get {
                return State.ShopName;
            }
        }
    }
}
