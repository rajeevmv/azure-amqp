// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Amqp.Sasl
{
    using System;
    using System.Globalization;
    using System.Security.Principal;
    using System.Text;
    using System.Threading.Tasks;

    public sealed class SaslPlainHandler : SaslHandler
    {
        public static readonly string Name = "PLAIN";
        static readonly string InvalidCredential = "Invalid user name or password.";
        ISaslPlainAuthenticator authenticator;

        public SaslPlainHandler()
        {
            this.Mechanism = Name;
        }

        public SaslPlainHandler(ISaslPlainAuthenticator authenticator)
            : this()
        {
            this.authenticator = authenticator;
        }

        public string AuthorizationIdentity
        {
            get;
            set;
        }

        public string AuthenticationIdentity
        {
            get;
            set;
        }

        public string Password
        {
            get;
            set;
        }

        public override SaslHandler Clone()
        {
            return new SaslPlainHandler(this.authenticator)
            {
                AuthorizationIdentity = this.AuthorizationIdentity,
                AuthenticationIdentity = this.AuthenticationIdentity,
                Password = this.Password
            };
        }

        public override void OnChallenge(SaslChallenge challenge)
        {
            throw new NotImplementedException();
        }

        public override void OnResponse(SaslResponse response)
        {
            throw new NotImplementedException();
        }

        protected override void OnStart(SaslInit init, bool isClient)
        {
            if (isClient)
            {
                string message = this.GetClientMessage();
                init.InitialResponse = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));
                this.Negotiator.WriteFrame(init, true);
            }
            else
            {
                this.OnInit(init);
            }
        }

        void OnInit(SaslInit init)
        {
            // the client message is specified by RFC4616
            // message = [authzid] UTF8NUL authcid UTF8NUL passwd
            // authcid and passwd should be prepared [SASLPrep] before
            // the verification process.
            string password = null;
            if (init.InitialResponse.Count > 0)
            {
                string message = Encoding.UTF8.GetString(init.InitialResponse.Array, init.InitialResponse.Offset, init.InitialResponse.Count);
                string[] items = message.Split('\0');
                if (items.Length != 3)
                {
                    throw new UnauthorizedAccessException(SaslPlainHandler.InvalidCredential);
                }

                this.AuthorizationIdentity = items[0];
                this.AuthenticationIdentity = items[1];
                password = items[2];
            }

            if (string.IsNullOrEmpty(this.AuthenticationIdentity))
            {
                throw new UnauthorizedAccessException(SaslPlainHandler.InvalidCredential);
            }

            if (this.authenticator != null)
            {
                this.authenticator.AuthenticateAsync(this.AuthenticationIdentity, password).ContinueWith((t) => this.CompleteNegotiation(t), TaskContinuationOptions.ExecuteSynchronously);
            }
        }

        void CompleteNegotiation(Task<IPrincipal> authenticateTask)
        {
            if (authenticateTask.IsFaulted)
            {
                this.Negotiator.CompleteNegotiation(SaslCode.Sys, authenticateTask.Exception);
            }
            else if (authenticateTask.IsCanceled)
            {
                this.Negotiator.CompleteNegotiation(SaslCode.Sys, new OperationCanceledException());
            }
            else
            {
                this.Principal = authenticateTask.Result;
                this.Negotiator.CompleteNegotiation(SaslCode.Ok, null);
            }
        }

        string GetClientMessage()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}\0{1}\0{2}", this.AuthorizationIdentity, this.AuthenticationIdentity, this.Password);
        }
    }
}
