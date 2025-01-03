using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

public class ConsoleDiagnosticListener(ILogger logger) : IObserver<DiagnosticListener>
{
    public void OnNext(DiagnosticListener value)
    {
        // Subscribe to "HttpHandlerDiagnosticListener"
        if (value.Name == "HttpHandlerDiagnosticListener")
        {
            value.Subscribe(new HttpHandlerObserver(logger));
        }
    }
    public void OnError(Exception error) { }
    public void OnCompleted() { }

    private class HttpHandlerObserver(ILogger logger) : IObserver<KeyValuePair<string, object>>
    {
        public void OnNext(KeyValuePair<string, object> kvPair)
        {
            // Log the request URL
            if (kvPair.Key == "System.Net.Http.HttpRequestOut.Start")
            {
                LogRequestUrl(kvPair);
            }
            // Log the response body (decoded)
            else if (kvPair.Key == "System.Net.Http.HttpRequestOut.Stop")
            {
                LogResponseBody(kvPair);
            }
        }

        public void OnError(Exception ex)
        {
            logger.LogInformation("HttpHandlerObserver Error: {exMessage}", ex.Message);
        }

        public void OnCompleted()
        {
            logger.LogInformation("HttpHandlerObserver Completed");
        }

        private void LogRequestUrl(KeyValuePair<string, object> kvPair)
        {
            try
            {
                if (kvPair.Value != null)
                {
                    // Use reflection to get the HttpRequestMessage
                    var requestProperty = kvPair.Value.GetType().GetProperty("Request");
                    if (requestProperty != null)
                    {
                        if (requestProperty.GetValue(kvPair.Value) is HttpRequestMessage httpRequest)
                        {
                            logger.LogInformation("Request URL: {requestUri}", httpRequest.RequestUri);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogInformation("Error logging request URL: {exMessage}", ex.Message);
            }
        }

        private void LogResponseBody(KeyValuePair<string, object> kvPair)
        {
            try
            {
                if (kvPair.Value != null)
                {
                    // Use reflection to get the HttpResponseMessage
                    var responseProperty = kvPair.Value.GetType().GetProperty("Response");
                    if (responseProperty != null)
                    {
                        if (responseProperty.GetValue(kvPair.Value) is HttpResponseMessage httpResponse)
                        {
                            // Read and decode the response body (async)
                            Task.Run(async () =>
                            {
                                string decodedResponse = await GetDecodedResponseBody(httpResponse);
                                string prettyResponse = TryPrettyPrintJson(decodedResponse);

                                logger.LogInformation("Decoded Response Body: {decodedResponse}", prettyResponse);
                            }).Wait(); // Wait to ensure async execution is completed
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogInformation("Error logging response body: {exMessage}", ex.Message );
            }
        }

        private async Task<string> GetDecodedResponseBody(HttpResponseMessage httpResponse)
        {
            // Buffer the content so the stream can be reused
            var originalContent = httpResponse.Content;
            var rawBytes = await originalContent.ReadAsByteArrayAsync();

            // Replace the original HTTP content with a buffered, memory-based content
            httpResponse.Content = new StreamContent(new MemoryStream(rawBytes));

            // Get content encoding for decoding (if needed)
            var contentEncoding = originalContent.Headers.ContentEncoding;

            // Add back headers to the new StreamContent
            foreach (var header in originalContent.Headers)
            {
                httpResponse.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            // Create decompression stream if the content is compressed
            Stream decompressedStream = new MemoryStream(rawBytes); // Default: assume no compression
            if (contentEncoding.Contains("gzip"))
            {
                decompressedStream = new GZipStream(new MemoryStream(rawBytes), CompressionMode.Decompress);
            }
            else if (contentEncoding.Contains("br"))
            {
                decompressedStream = new BrotliStream(new MemoryStream(rawBytes), CompressionMode.Decompress);
            }

            // Read and return the response body as a plain string
            using var reader = new StreamReader(decompressedStream, Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }
 
        private string TryPrettyPrintJson(string rawText)
        {
            try
            {
                // Try to parse and pretty-print the response as JSON
                var jsonElement = System.Text.Json.JsonDocument.Parse(rawText).RootElement;
                return System.Text.Json.JsonSerializer.Serialize(jsonElement, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
            }
            catch
            {
                // Return the original text if it's not valid JSON
                return rawText;
            }
        }
    }
}