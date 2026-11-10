using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace TLA.Systems.Dialogue.Online
{
    public enum TransportStatus
    {
        Success,     // got an HTTP 2xx with a body
        Offline,     // no network, DNS failure, connection refused
        Timeout,
        HttpError,   // got an HTTP error status
        Cancelled
    }

    public readonly struct TransportResult
    {
        public readonly TransportStatus Status;
        public readonly long HttpCode;
        public readonly string Body;
        public readonly string Error;

        public TransportResult(TransportStatus status, long httpCode = 0, string body = null, string error = null)
        {
            Status = status;
            HttpCode = httpCode;
            Body = body;
            Error = error;
        }
    }

    /// <summary>
    /// Sends one JSON POST. Separate from <see cref="OnlineAIProvider"/> so tests can simulate
    /// timeouts, disconnections and bad servers without a network.
    /// </summary>
    public interface IDialogueTransport
    {
        Task<TransportResult> PostJsonAsync(string url, string json, float timeoutSeconds, CancellationToken ct);
    }

    /// <summary>The real transport: UnityWebRequest (works in the Editor and in Windows builds).</summary>
    public sealed class UnityWebRequestTransport : IDialogueTransport
    {
        /// <summary>Largest reply body accepted (a spoken line is tiny; anything bigger is wrong).</summary>
        private const int MaxBodyBytes = 16 * 1024;

        public async Task<TransportResult> PostJsonAsync(string url, string json, float timeoutSeconds, CancellationToken ct)
        {
            // Fail fast with no network at all (localhost still works offline, so don't block it).
            if (Application.internetReachability == NetworkReachability.NotReachable && !BackendUrl.IsLocal(url))
                return new TransportResult(TransportStatus.Offline, error: "No internet connection.");

            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = Mathf.Max(1, Mathf.CeilToInt(timeoutSeconds)),
                redirectLimit = 0 // never follow redirects to somewhere else
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");

            var done = new TaskCompletionSource<bool>();
            UnityWebRequestAsyncOperation op = request.SendWebRequest();
            op.completed += _ => done.TrySetResult(true);

            using (ct.Register(() => done.TrySetCanceled()))
            {
                try
                {
                    await done.Task;
                }
                catch (OperationCanceledException)
                {
                    request.Abort();
                    return new TransportResult(TransportStatus.Cancelled, error: "Cancelled.");
                }
            }

            string body = request.downloadHandler?.text;
            if (body != null && body.Length > MaxBodyBytes) body = null;

            switch (request.result)
            {
                case UnityWebRequest.Result.Success:
                    return body == null
                        ? new TransportResult(TransportStatus.HttpError, request.responseCode, null, "Reply too large.")
                        : new TransportResult(TransportStatus.Success, request.responseCode, body);

                case UnityWebRequest.Result.ProtocolError:
                    return new TransportResult(TransportStatus.HttpError, request.responseCode, body, request.error);

                default: // ConnectionError, DataProcessingError
                    bool timedOut = request.error != null && request.error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0;
                    return new TransportResult(timedOut ? TransportStatus.Timeout : TransportStatus.Offline, request.responseCode, null, request.error);
            }
        }
    }

    /// <summary>Checks that the backend address is safe to call.</summary>
    public static class BackendUrl
    {
        /// <summary>
        /// Builds the dialogue endpoint, or returns null with a reason. Rules: must be an absolute
        /// http(s) URL; plain http only for this computer (localhost / 127.0.0.1); no user:password@.
        /// </summary>
        public static string ToEndpoint(string baseUrl, out string problem)
        {
            problem = null;
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                problem = "No backend address is configured.";
                return null;
            }

            if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out Uri uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                problem = "The backend address isn't a valid http(s) URL.";
                return null;
            }

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                problem = "The backend address must not contain credentials.";
                return null;
            }

            if (uri.Scheme == Uri.UriSchemeHttp && !uri.IsLoopback)
            {
                problem = "The backend must use https (http is only allowed for localhost).";
                return null;
            }

            return uri.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/v1/dialogue";
        }

        public static bool IsLocal(string url) => Uri.TryCreate(url, UriKind.Absolute, out Uri uri) && uri.IsLoopback;
    }
}
