using Microsoft.Bot.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Teams.AI.Exceptions;
using Microsoft.Teams.AI.State;

namespace Microsoft.Teams.AI
{
    /// <summary>
    /// Handles authentication based on Teams SSO.
    /// </summary>
    public class TeamsSsoAuthentication<TState> : IAuthentication<TState>
        where TState : TurnState, new()
    {
        internal IConfidentialClientApplicationAdapter _msalAdapter;

        internal TeamsSsoBotAuthentication<TState>? _botAuth;
        private TeamsSsoMessageExtensionsAuthentication? _messageExtensionsAuth;
        private TeamsSsoSettings _settings;
        private ILogger _logger;

        /// <summary>
        /// Initialize instance for current class
        /// </summary>
        /// <param name="app">The application.</param>
        /// <param name="name">The authentication name.</param>
        /// <param name="settings">The settings to initialize the class</param>
        /// <param name="storage">The storage to use.</param>
        /// <param name="redoUserTaskAsync">Function to continue user action after SSO</param>
        /// <param name="logger">logger</param>
        public TeamsSsoAuthentication(Application<TState> app, string name, TeamsSsoSettings settings, IStorage? storage = null, Func<Task> redoUserTaskAsync = null, ILogger logger = null)
        {
            _settings = settings;
            _logger = logger;
            _botAuth = new TeamsSsoBotAuthentication<TState>(app, name, _settings, storage, redoUserTaskAsync);
            _messageExtensionsAuth = new TeamsSsoMessageExtensionsAuthentication(_settings);
            _msalAdapter = new ConfidentialClientApplicationAdapter(settings.MSAL);
        }

        /// <summary>
        /// Sign in current user
        /// </summary>
        /// <param name="context">The turn context</param>
        /// <param name="state">The turn state</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The sign in response</returns>
        public async Task<string?> SignInUserAsync(ITurnContext context, TState state, CancellationToken cancellationToken = default)
        {
            string token = await _TryGetUserToken(context);
            if (!string.IsNullOrEmpty(token))
            {
                return token;
            }

            if ((_botAuth != null && _botAuth.IsValidActivity(context)))
            {
                return await _botAuth.AuthenticateAsync(context, state);
            }

            if ((_messageExtensionsAuth != null && _messageExtensionsAuth.IsValidActivity(context)))
            {
                return await _messageExtensionsAuth.AuthenticateAsync(context);
            }

            throw new AuthException("Incoming activity is not a valid activity to initiate authentication flow.", AuthExceptionReason.InvalidActivity);
        }

        /// <summary>
        /// Sign out current user
        /// </summary>
        /// <param name="context">The turn context</param>
        /// <param name="state">The turn state</param>
        /// <param name="cancellationToken">The cancellation token</param>
        public async Task SignOutUserAsync(ITurnContext context, TState state, CancellationToken cancellationToken = default)
        {
            string homeAccountId = $"{context.Activity.From.AadObjectId}.{context.Activity.Conversation.TenantId}";

            await _msalAdapter.StopLongRunningProcessInWebApiAsync(homeAccountId, cancellationToken);
        }

        /// <summary>
        /// The handler function is called when the user has successfully signed in
        /// </summary>
        /// <param name="handler">The handler function to call when the user has successfully signed in</param>
        /// <returns>The class itself for chaining purpose</returns>
        public IAuthentication<TState> OnUserSignInSuccess(Func<ITurnContext, TState, Task> handler)
        {
            if (_botAuth != null)
            {
                _botAuth.OnUserSignInSuccess(handler);
            }
            return this;
        }

        /// <summary>
        /// The handler function is called when the user sign in flow fails
        /// </summary>
        /// <param name="handler">The handler function to call when the user failed to signed in</param>
        /// <returns>The class itself for chaining purpose</returns>
        public IAuthentication<TState> OnUserSignInFailure(Func<ITurnContext, TState, AuthException, Task> handler)
        {
            if (_botAuth != null)
            {
                _botAuth.OnUserSignInFailure(handler);
            }
            return this;
        }

        /// <summary>
        /// Check if the user is signed, if they are then return the token.
        /// </summary>
        /// <param name="context">The turn context.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The token if the user is signed. Otherwise null.</returns>
        public async Task<string?> IsUserSignedInAsync(ITurnContext context, CancellationToken cancellationToken = default)
        {
            string token = await _TryGetUserToken(context);
            return token == "" ? null : token;
        }

        private async Task<string> _TryGetUserToken(ITurnContext context)
        {
            string homeAccountId = $"{context.Activity.From.AadObjectId}.{context.Activity.Conversation.TenantId}";
            try
            {
                AuthenticationResult result = await _msalAdapter.AcquireTokenInLongRunningProcess(_settings.Scopes, homeAccountId);
                return result.AccessToken;
            }
            catch (MsalClientException ex)
            {
                // Cannot acquire token from cache
                _logger?.LogInformation($"Failed to acquire token. Error: {ex.Message}");
            }

            return ""; // Return empty indication no token found in cache
        }
    }
}
