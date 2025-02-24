﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Protocols.TestTools.StackSdk.Asn1;
using Microsoft.Protocols.TestTools.StackSdk.Security.Cryptographic;
using Microsoft.Protocols.TestTools.StackSdk.Security.SspiLib;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using EventSource = Microsoft.Protocols.TestTools.StackSdk.Security.KerberosLib.EventSources.KerberosEventSource;

namespace Microsoft.Protocols.TestTools.StackSdk.Security.KerberosLib
{
    public class KerberosClientSecurityContext : ClientSecurityContext
    {

        #region Fields

        /// <summary>
        /// the Kerberos client to create the Kerberos packet and holds the context and config.
        /// </summary>
        private KerberosClient client;

        /// <summary>
        /// the credential of user to authenticate.
        /// </summary>
        private AccountCredential credential;

        /// <summary>
        /// The token returned by Sspi.
        /// </summary>
        private byte[] token;

        /// <summary>
        /// Context attribute flags.
        /// </summary>
        private ClientSecurityContextAttribute contextAttribute;

        /// <summary>
        /// Whether to continue process.
        /// </summary>
        private bool needContinueProcessing;

        private string serverName;

        /// <summary>
        /// Queries the sizes of the structures used in the per-message functions.
        /// </summary>
        private SecurityPackageContextSizes contextSizes;

        /// <summary>
        /// KRB_AP_REQ Authenticator
        /// </summary>
        private Authenticator ApRequestAuthenticator;

        private ReaderWriterLockSlim cacheLock = new ReaderWriterLockSlim();

        /// <summary>
        /// store not expired TGT token for Reauthentication
        /// </summary>
        private static Dictionary<CacheTokenKey, KerberosTicket> TGTTokenCache = new Dictionary<CacheTokenKey, KerberosTicket>();
        #endregion

        #region Properties

        /// <summary>
        /// the credential of user to authenticate.
        /// </summary>
        public AccountCredential Credential
        {
            get
            {
                return this.credential;
            }
            set
            {
                this.credential = value;
            }
        }

        /// <summary>
        /// the context of kerberos client. set the negotiate flags.
        /// </summary>
        public KerberosContext Context
        {
            get
            {
                return this.client.Context;
            }
        }

        /// <summary>
        /// Whether to continue process.
        /// </summary>
        public override bool NeedContinueProcessing
        {
            get
            {
                return this.needContinueProcessing;
            }
        }

        /// <summary>
        /// The session key that generated by sdk.
        /// </summary>
        public override byte[] SessionKey
        {
            get
            {
                if ((this.client.Context.SessionKey != null)
                    && (this.client.Context.SessionKey.keyvalue != null)
                    )
                    return this.client.Context.SessionKey.keyvalue.ByteArrayValue;
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// The token returned by Sspi.
        /// </summary>
        public override byte[] Token
        {
            get
            {
                return this.token;
            }
        }

        /// <summary>
        /// Gets or sets sequence number for Verify, Encrypt and Decrypt message.
        /// For Digest SSP, it must be 0.
        /// </summary>
        public override uint SequenceNumber
        {
            get
            {
                return this.Context.CurrentLocalSequenceNumber;
            }
            set
            {
                this.Context.currentLocalSequenceNumber = value;
            }
        }

        /// <summary>
        /// Package type
        /// </summary>
        public override SecurityPackageType PackageType
        {
            get
            {
                return SecurityPackageType.Kerberos;
            }
        }

        public override SecurityPackageContextSizes ContextSizes
        {
            get
            {
                return this.contextSizes;
            }
        }

        public string ServerName
        {
            get { return this.serverName; }
        }

        public override object QueryContextAttributes(string contextAttribute)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region Constructors

        /// <summary>
        /// Create a KerberosClientSecurityContext instance.
        /// </summary>
        /// <param name="domain" cref="KerberosClientCredential">Login user Credential</param>
        /// <param name="accountType">The type of the logon account. User or Computer</param>
        /// <param name="kdcAddress">The IP address of the KDC.</param>
        /// <param name="kdcPort">The port of the KDC.</param>
        /// <param name="transportType">Whether the transport is TCP or UDP transport.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the input parameter is null.</exception>
        public KerberosClientSecurityContext(
            string serverName,
            AccountCredential credential,
            KerberosAccountType accountType,
            IPAddress kdcAddress,
            int kdcPort,
            TransportType transportType,
            ClientSecurityContextAttribute contextAttribute,
            KerberosConstValue.OidPkt oidPkt = KerberosConstValue.OidPkt.KerberosToken,
            string salt = null
            )
        {
            EventSource.Log.ClientSecurityContext_Construction_I_Started(serverName, credential?.DomainName,
                credential?.AccountName, accountType.ToString(), kdcAddress.ToString(), kdcPort, transportType.ToString(),
                contextAttribute.ToString(), oidPkt.ToString(), salt);

            this.credential = credential;
            this.serverName = serverName;
            this.contextAttribute = contextAttribute;
            this.client = new KerberosClient(this.credential.DomainName, this.credential.AccountName, this.credential.Password, accountType, kdcAddress, kdcPort, transportType, oidPkt, salt);
            this.UpdateDefaultSettings();

            EventSource.Log.ClientSecurityContext_Construction_I_Completed();
        }

        /// <summary>
        /// Generate a new NlmpClient Security Context
        /// </summary>
        /// <param name="domain" cref="KerberosClientCredential">Login user Credential</param>
        /// <param name="accountType">The type of the logon account. User or Computer</param>
        /// <param name="kdcAddress">The IP address of the KDC.</param>
        /// <param name="kdcPort">The port of the KDC.</param>
        /// <param name="transportType">Whether the transport is TCP or UDP transport.</param>
        /// <returns></returns>
        public static ClientSecurityContext CreateClientSecurityContext(
            string serverName,
            AccountCredential credential,
            KerberosAccountType accountType,
            IPAddress kdcAddress,
            int kdcPort,
            TransportType transportType,
            ClientSecurityContextAttribute contextAttribute,
            KerberosConstValue.OidPkt oidPkt = KerberosConstValue.OidPkt.KerberosToken,
            string salt = null
            )
        {
            return new KerberosClientSecurityContext(serverName, credential, accountType, kdcAddress, kdcPort, transportType, contextAttribute, oidPkt, salt);
        }

        #endregion

        #region Gss Api

        /// <summary>
        /// Initialize the context from a token.
        /// </summary>
        /// <param name="serverToken">the token from server</param>
        public override void Initialize(byte[] serverToken)
        {
            if (serverToken == null)
            {
                ClientInitialize();
            }
            else
            {
                ClientInitialize(serverToken);
            }
        }

        public override bool Decrypt(params SecurityBuffer[] securityBuffers)
        {
            return KerberosUtility.Decrypt(client, securityBuffers);
        }

        public override void Encrypt(params SecurityBuffer[] securityBuffers)
        {
            KerberosUtility.Encrypt(client, securityBuffers);
        }

        public override void Sign(params SecurityBuffer[] securityBuffers)
        {
            KerberosUtility.Sign(client, securityBuffers);
        }

        public override bool Verify(params SecurityBuffer[] securityBuffers)
        {
            return KerberosUtility.Verify(client, securityBuffers);
        }

        /// <summary>
        /// Kerberos Client Initialize without server token
        /// </summary>
        private void ClientInitialize()
        {
            EventSource.Log.ClientSecurityContext_InitializationWithoutServerTokenStarted();

            this.ApRequestAuthenticator = null;
            // Create and send AS request for pre-authentication
            KdcOptions options = KdcOptions.FORWARDABLE | KdcOptions.CANONICALIZE | KdcOptions.RENEWABLE;

            KerberosTicket ticket = this.GetTGTCachedToken(this.credential, this.serverName);
            if (ticket == null)
            {
                this.SendAsRequest(options, null);

                // Expect recieve preauthentication required error
                METHOD_DATA methodData;
                this.ExpectPreauthRequiredError(out methodData);

                // Create sequence of PA data
                string timeStamp = KerberosUtility.CurrentKerberosTime.Value;
                PaEncTimeStamp paEncTimeStamp = new PaEncTimeStamp(timeStamp,
                    0,
                    this.Context.SelectedEType,
                    this.Context.CName.Password,
                    this.Context.CName.Salt);
                PaPacRequest paPacRequest = new PaPacRequest(true);
                PaPacOptions paPacOptions = new PaPacOptions(PacOptions.Claims | PacOptions.ForwardToFullDc);
                Asn1SequenceOf<PA_DATA> seqOfPaData_AS = new Asn1SequenceOf<PA_DATA>(new PA_DATA[] { paEncTimeStamp.Data, paPacRequest.Data, paPacOptions.Data });
                // Create and send AS request for TGT
                KerberosAsRequest asRequest = this.SendAsRequest(options, seqOfPaData_AS);

                // Expect TGT(AS) Response from KDC
                KerberosAsResponse asResponse = this.ExpectAsResponse();

                // Create and send TGS request
                Asn1SequenceOf<PA_DATA> seqOfPaData_TGS = new Asn1SequenceOf<PA_DATA>(new PA_DATA[] { paPacRequest.Data, paPacOptions.Data });
                this.SendTgsRequest(this.serverName, options, seqOfPaData_TGS);

                // Expect TGS Response from KDC
                KerberosTgsResponse tgsResponse = this.ExpectTgsResponse();
                this.UpdateTGTCachedToken(this.Context.Ticket);
            }
            else
            {
                // Restore SessionKey and Ticket from cache
                this.Context.SessionKey = ticket.SessionKey;
                this.Context.ApSessionKey = ticket.SessionKey;
                this.Context.Ticket = ticket;
                this.Context.SelectedEType = (EncryptionType)Context.Ticket.Ticket.enc_part.etype.Value;
            }

            // cache this.Context.Ticket;
            ApOptions apOption;
            GetFlagsByContextAttribute(out apOption);

            AuthorizationData data = null;
            EncryptionKey subkey = KerberosUtility.GenerateKey(this.client.Context.ContextKey);

            this.token = this.CreateGssApiToken(apOption,
                data,
                subkey,
                this.Context.ChecksumFlag,
                KerberosConstValue.GSSToken.GSSAPI);

            bool isMutualAuth = (contextAttribute & ClientSecurityContextAttribute.MutualAuth)
                == ClientSecurityContextAttribute.MutualAuth;
            bool isDceStyle = (contextAttribute & ClientSecurityContextAttribute.DceStyle)
                == ClientSecurityContextAttribute.DceStyle;

            if (isMutualAuth || isDceStyle)
            {
                this.needContinueProcessing = true;
            }
            else
            {
                this.Context.SessionKey = this.Context.ApSubKey;
                this.needContinueProcessing = false;
            }

            EventSource.Log.ClientSecurityContext_InitializationWithoutServerTokenCompleted();
        }

        /// <summary>
        /// Client initialize with server token
        /// </summary>
        /// <param name="serverToken">Server token</param>
        private void ClientInitialize(byte[] serverToken)
        {
            EventSource.Log.ClientSecurityContext_InitializationWithServerTokenStarted(serverToken);

            KerberosApResponse apRep = this.GetApResponseFromToken(serverToken, KerberosConstValue.GSSToken.GSSAPI);
            this.VerifyApResponse(apRep);

            token = null;
            if ((contextAttribute & ClientSecurityContextAttribute.DceStyle) == ClientSecurityContextAttribute.DceStyle)
            {
                KerberosApResponse apResponse = this.CreateApResponse(null);

                var apBerBuffer = new Asn1BerEncodingBuffer();

                if (apResponse.ApEncPart != null)
                {
                    // Encode enc_part
                    apResponse.ApEncPart.BerEncode(apBerBuffer, true);

                    EncryptionKey key = this.Context.ApSessionKey;

                    if (key == null || key.keytype == null || key.keyvalue == null || key.keyvalue.Value == null)
                    {
                        throw new ArgumentException("Ap session key is not valid");
                    }

                    // Encrypt enc_part
                    EncryptionType eType = (EncryptionType)key.keytype.Value;
                    byte[] cipherData = KerberosUtility.Encrypt(
                        eType,
                        key.keyvalue.ByteArrayValue,
                        apBerBuffer.Data,
                        (int)KeyUsageNumber.AP_REP_EncAPRepPart);
                    apResponse.Response.enc_part = new EncryptedData(new KerbInt32((int)eType), null, new Asn1OctetString(cipherData));
                }

                // Encode AP Response
                apResponse.Response.BerEncode(apBerBuffer, true);

                if ((this.Context.ChecksumFlag & ChecksumFlags.GSS_C_DCE_STYLE) == ChecksumFlags.GSS_C_DCE_STYLE)
                {
                    // In DCE mode, the AP-REP message MUST NOT have GSS-API wrapping. 
                    // It is sent as is without encapsulating it in a header ([RFC2743] section 3.1).
                    this.token = apBerBuffer.Data;
                }
                else
                {
                    this.token = KerberosUtility.AddGssApiTokenHeader(ArrayUtility.ConcatenateArrays(
                        BitConverter.GetBytes(KerberosUtility.ConvertEndian((ushort)TOK_ID.KRB_AP_REP)),
                        apBerBuffer.Data));
                }
            }

            this.needContinueProcessing = false;      // SEC_E_OK;

            EventSource.Log.ClientSecurityContext_InitializationWithServerTokenCompleted();
        }
        #endregion

        #region AS
        private KerberosAsRequest SendAsRequest(KdcOptions kdcOptions, Asn1SequenceOf<PA_DATA> seqPaData)
        {
            string sName = KerberosConstValue.KERBEROS_SNAME;
            string domain = this.Context.Realm.Value;
            PrincipalName sname =
                new PrincipalName(new KerbInt32((int)PrincipalType.NT_SRV_INST), KerberosUtility.String2SeqKerbString(sName, domain));

            KDC_REQ_BODY kdcReqBody = CreateKdcRequestBody(kdcOptions, sname);
            KerberosAsRequest asRequest = this.CreateAsRequest(kdcReqBody, seqPaData);
            this.client.SendPdu(asRequest);
            return asRequest;
        }

        private KerberosAsRequest CreateAsRequest(KDC_REQ_BODY kdcReqBody, Asn1SequenceOf<PA_DATA> paDatas, long pvno = KerberosConstValue.KERBEROSV5)
        {
            KerberosAsRequest request = new KerberosAsRequest(
                pvno,
                kdcReqBody,
                paDatas,
                Context.TransportType);
            return request;
        }

        /// <summary>
        /// Expected a KDC_ERR_PREAUTH_REQUIRED error
        /// </summary>
        /// <param name="eData">Error data of this error</param>
        /// <returns></returns>
        private KerberosKrbError ExpectPreauthRequiredError(out METHOD_DATA eData)
        {
            KerberosPdu responsePdu = this.client.ExpectPdu(KerberosConstValue.TIMEOUT_DEFAULT, typeof(KerberosKrbError));
            if ((responsePdu == null) || (!(responsePdu is KerberosKrbError)))
            {
                throw new InvalidOperationException("Response type should be KerberosKrbError");
            }

            KerberosKrbError krbError = responsePdu as KerberosKrbError;
            if (!krbError.ErrorCode.Equals(KRB_ERROR_CODE.KDC_ERR_PREAUTH_REQUIRED))
            {
                throw new InvalidOperationException("The Error code should be KDC_ERR_PREAUTH_REQUIRED");
            }

            eData = new METHOD_DATA();
            Asn1DecodingBuffer buffer = new Asn1DecodingBuffer(krbError.KrbError.e_data.ByteArrayValue);
            eData.BerDecode(buffer);
            return krbError;
        }

        private KerberosAsResponse ExpectAsResponse()
        {
            KerberosPdu responsePdu = this.client.ExpectPdu(KerberosConstValue.TIMEOUT_DEFAULT, typeof(KerberosAsResponse));
            if (responsePdu == null)
            {
                throw new Exception("Expected KerberosAsResponse data is null");
            }

            if (responsePdu is KerberosKrbError)
            {
                KerberosKrbError errorResponse = responsePdu as KerberosKrbError;
                throw new Exception($"Expected KerberosAsResponse failed, Error Code:{errorResponse.ErrorCode}");
            }
            else if (!(responsePdu is KerberosAsResponse))
            {
                throw new Exception($"Expected KerberosAsResponse failed, reponse type is not a valid KerberosAsResponse, Response Type:{responsePdu.GetType().ToString()}");
            }

            KerberosAsResponse response = responsePdu as KerberosAsResponse;
            response.Decrypt(this.Context.ReplyKey.keyvalue.ByteArrayValue);
            return response;
        }
        #endregion

        #region TGS

        private void SendTgsRequest(string sName, KdcOptions kdcOptions, Asn1SequenceOf<PA_DATA> seqPadata = null, AuthorizationData dataInAuthentiator = null, AuthorizationData dataInEncAuthData = null, MsgType msgType = MsgType.KRB_TGS_REQ)
        {
            if (string.IsNullOrEmpty(sName))
            {
                throw new ArgumentNullException("sName");
            }
            PrincipalName sname = new PrincipalName(new KerbInt32((int)PrincipalType.NT_SRV_INST), KerberosUtility.String2SeqKerbString(sName.Split('/')));

            KDC_REQ_BODY kdcReqBody = this.CreateKdcRequestBody(kdcOptions, sname, dataInEncAuthData); // almost same as AS request
            Asn1BerEncodingBuffer bodyBuffer = new Asn1BerEncodingBuffer();
            kdcReqBody.BerEncode(bodyBuffer);

            ChecksumType checksumType = KerberosUtility.GetChecksumType(this.Context.SelectedEType);
            PA_DATA paTgsReq = CreatePaTgsReqest(checksumType, bodyBuffer.Data, dataInAuthentiator); // use AS session key encrypt authenticator.

            Asn1SequenceOf<PA_DATA> tempPaData = null;
            if (seqPadata == null || seqPadata.Elements == null || seqPadata.Elements.Length == 0)
            {
                tempPaData = new Asn1SequenceOf<PA_DATA>(new PA_DATA[] { paTgsReq });
            }
            else
            {
                PA_DATA[] paDatas = new PA_DATA[seqPadata.Elements.Length + 1];
                Array.Copy(seqPadata.Elements, paDatas, seqPadata.Elements.Length);
                paDatas[seqPadata.Elements.Length] = paTgsReq;
                tempPaData = new Asn1SequenceOf<PA_DATA>(paDatas);
            }

            KerberosTgsRequest tgsRequest = new KerberosTgsRequest(KerberosConstValue.KERBEROSV5, kdcReqBody, tempPaData, Context.TransportType);
            tgsRequest.Request.msg_type.Value = (long)msgType;
            this.client.SendPdu(tgsRequest);
        }

        private KerberosTgsResponse ExpectTgsResponse(KeyUsageNumber usage = KeyUsageNumber.TGS_REP_encrypted_part)
        {
            var response = this.client.ExpectPdu(KerberosConstValue.TIMEOUT_DEFAULT, typeof(KerberosTgsResponse));
            if (response == null || !(response is KerberosTgsResponse))
            {
                throw new Exception("Expected KerberosAsResponse data is null");
            }

            KerberosTgsResponse tgsResponse = response as KerberosTgsResponse;
            if (this.Context.ReplyKey == null)
            {
                throw new Exception("Reply key is null");
            }

            tgsResponse.DecryptTgsResponse(this.Context.ReplyKey.keyvalue.ByteArrayValue, usage);
            return tgsResponse;
        }

        #endregion

        #region AP

        /// <summary>
        /// Create AP request and encode to GSSAPI token
        /// </summary>
        /// <param name="apOptions">AP options</param>
        /// <param name="data">Authorization data</param>
        /// <param name="subkey">Sub-session key in authenticator</param>
        /// <param name="checksumFlags">Checksum flags</param>
        /// <returns></returns>
        private byte[] CreateGssApiToken(ApOptions apOptions, AuthorizationData data, EncryptionKey subkey, ChecksumFlags checksumFlags, KerberosConstValue.GSSToken gssToken = KerberosConstValue.GSSToken.GSSSPNG)
        {
            APOptions options = new APOptions(KerberosUtility.ConvertInt2Flags((int)apOptions));

            Authenticator authenticator = CreateAuthenticator(Context.Ticket, data, subkey, checksumFlags);
            this.ApRequestAuthenticator = authenticator;
            KerberosApRequest request = new KerberosApRequest(
                Context.Pvno,
                options,
                Context.Ticket,
                authenticator,
                KeyUsageNumber.AP_REQ_Authenticator
                );
            this.client.UpdateContext(request);

            if ((this.Context.ChecksumFlag & ChecksumFlags.GSS_C_DCE_STYLE) == ChecksumFlags.GSS_C_DCE_STYLE)
            {
                return request.ToBytes();
            }
            else
            {
                return KerberosUtility.AddGssApiTokenHeader(request, this.client.OidPkt, gssToken);
            }
        }

        /// <summary>
        /// Decode GSSAPI token to AP-REP
        /// </summary>
        /// <param name="token">GSSAPI token</param>
        /// <returns></returns>
        private KerberosApResponse GetApResponseFromToken(byte[] token, KerberosConstValue.GSSToken gssToken = KerberosConstValue.GSSToken.GSSSPNG)
        {
            EventSource.Log.ClientSecurityContext_GetApResponseFromTokenStarted
                (token, gssToken.ToString());

            if (gssToken == KerberosConstValue.GSSToken.GSSSPNG)
                token = KerberosUtility.DecodeNegotiationToken(token);

            if (token[0] == KerberosConstValue.KERBEROS_TAG)
            {
                byte[] apData = KerberosUtility.VerifyGssApiTokenHeader(token, this.client.OidPkt);

                // Check if it has a two-byte tok_id
                if (null == apData || apData.Length <= sizeof(TOK_ID))
                {
                    throw new FormatException(
                        "Data length is shorter than a valid AP Response data length.");
                }

                // verify TOK_ID
                byte[] tokenID = ArrayUtility.SubArray<byte>(apData, 0, sizeof(TOK_ID));
                Array.Reverse(tokenID);
                TOK_ID id = (TOK_ID)BitConverter.ToUInt16(tokenID, 0);

                if (!id.Equals(TOK_ID.KRB_AP_REP))
                {
                    throw new Exception("ApResponse Token ID should be KRB_AP_REP");
                }

                // Get apBody
                token = ArrayUtility.SubArray(apData, sizeof(TOK_ID));
            }

            KerberosApResponse apRep = new KerberosApResponse();
            apRep.FromBytes(token);

            // Get the current encryption type, cipher data
            EncryptionType encryptType = (EncryptionType)apRep.Response.enc_part.etype.Value;
            byte[] cipherData = apRep.Response.enc_part.cipher.ByteArrayValue;
            byte[] sessionKey = this.Context.ApSessionKey.keyvalue.ByteArrayValue;

            // decrypt enc_part to clear text
            byte[] clearText = KerberosUtility.Decrypt(encryptType, sessionKey, cipherData, (int)KeyUsageNumber.AP_REP_EncAPRepPart);

            // decode enc_part
            Asn1DecodingBuffer decodeBuffer = new Asn1DecodingBuffer(clearText);
            apRep.ApEncPart = new EncAPRepPart();
            apRep.ApEncPart.BerDecode(decodeBuffer);

            this.client.UpdateContext(apRep);

            EventSource.Log.ClientSecurityContext_GetApResponseFromTokenCompleted(token,
                encryptType.ToString(), cipherData, sessionKey, clearText);

            return apRep;
        }

        public KerberosApResponse CreateApResponse(EncryptionKey subkey)
        {
            var response = new KerberosApResponse();
            response.Response.msg_type = new Asn1Integer((int)MsgType.KRB_AP_RESP);
            response.Response.pvno = new Asn1Integer(KerberosConstValue.KERBEROSV5);

            // Set EncAPRepPart
            var apEncPart = new EncAPRepPart();
            apEncPart.ctime = Context.Time;
            apEncPart.cusec = Context.Cusec;
            apEncPart.subkey = null;
            apEncPart.seq_number = new KerbUInt32((long)Context.currentRemoteSequenceNumber);
            response.ApEncPart = apEncPart;

            this.client.UpdateContext(response);
            return response;
        }

        private void VerifyApResponse(KerberosApResponse apRep)
        {
            apRep.Decrypt(this.Context.ApSessionKey.keyvalue.ByteArrayValue);
            if (apRep.ApEncPart != null)
            {
                if (apRep.ApEncPart.ctime.Value != this.ApRequestAuthenticator.ctime.Value)
                {
                    throw new NotSupportedException("ctime is not match with KRB_AP_REQ ctime");
                }
                if (apRep.ApEncPart.cusec.Value != this.ApRequestAuthenticator.cusec.Value)
                {
                    throw new NotSupportedException("cusec is not match with KRB_AP_REQ cusec");
                }
                if (apRep.ApEncPart.seq_number.Value == null || apRep.ApEncPart.seq_number.Value <= 0)
                {
                    throw new NotSupportedException("seq_number should be a valid value");
                }
            }
            else
            {
                throw new NotSupportedException("KRB_AP_REP decrypt failed");
            }
        }
        #endregion

        #region Utility Methods

        private KDC_REQ_BODY CreateKdcRequestBody(KdcOptions kdcOptions, PrincipalName sName)
        {
            KerbUInt32 nonce = new KerbUInt32((uint)Math.Abs((int)DateTime.Now.Ticks));
            KerberosTime till = new KerberosTime(KerberosConstValue.TGT_TILL_TIME);
            KerberosTime rtime = new KerberosTime(KerberosConstValue.TGT_RTIME);
            HostAddresses addresses =
                new HostAddresses(new HostAddress[1] { new HostAddress(new KerbInt32((int)AddressType.NetBios),
                    new Asn1OctetString(Encoding.ASCII.GetBytes(System.Net.Dns.GetHostName()))) });

            KDCOptions options = new KDCOptions(KerberosUtility.ConvertInt2Flags((int)kdcOptions));

            KDC_REQ_BODY kdcReqBody = new KDC_REQ_BODY(options, Context.CName.Name, Context.Realm, sName, null, till, rtime, nonce, Context.SupportedEType, addresses, null, null);
            return kdcReqBody;
        }

        private KDC_REQ_BODY CreateKdcRequestBody(KdcOptions kdcOptions, PrincipalName sName, AuthorizationData authData = null)
        {
            KDC_REQ_BODY kdcReqBody = this.CreateKdcRequestBody(kdcOptions, sName);
            if (authData == null)
            {
                return kdcReqBody;
            }

            Asn1BerEncodingBuffer asnEncBuffer = new Asn1BerEncodingBuffer();
            authData.BerEncode(asnEncBuffer, true);

            EncryptedData encryptData = new EncryptedData();
            encryptData.etype = new KerbInt32(0);
            byte[] encryptAsnEncoded = asnEncBuffer.Data;
            if (this.Context.SessionKey != null && this.Context.SessionKey.keytype != null && this.Context.SessionKey.keyvalue != null && this.Context.SessionKey.keyvalue.Value != null)
            {
                encryptAsnEncoded = KerberosUtility.Encrypt(
                    (EncryptionType)this.Context.SessionKey.keytype.Value,
                    this.Context.SessionKey.keyvalue.ByteArrayValue,
                    encryptAsnEncoded,
                    (int)KeyUsageNumber.TGS_REQ_KDC_REQ_BODY_AuthorizationData
                );

                encryptData.etype = new KerbInt32(this.Context.SessionKey.keytype.Value);
            }
            encryptData.cipher = new Asn1OctetString(encryptAsnEncoded);
            kdcReqBody.enc_authorization_data = encryptData;

            return kdcReqBody;
        }

        private PA_DATA CreatePaTgsReqest(ChecksumType checksumType, byte[] checksumBody, AuthorizationData data)
        {
            APOptions option = new APOptions(KerberosUtility.ConvertInt2Flags((int)ApOptions.None));
            EncryptionKey key = Context.SessionKey;
            KerberosApRequest apRequest = CreateApRequest(option, Context.Ticket, null, data, KeyUsageNumber.TG_REQ_PA_TGS_REQ_padataOR_AP_REQ_Authenticator, checksumType, checksumBody);

            PaTgsReq paTgsReq = new PaTgsReq(apRequest.Request);
            return paTgsReq.Data;
        }

        private KerberosApRequest CreateApRequest(APOptions option, KerberosTicket ticket, EncryptionKey subKey, AuthorizationData data, KeyUsageNumber keyUsageNumber, ChecksumType checksumType, byte[] checksumBody)
        {
            Authenticator authenticator = CreateAuthenticator(ticket, data, subKey, checksumType, checksumBody);
            KerberosApRequest apRequest = new KerberosApRequest(Context.Pvno, option, ticket, authenticator, keyUsageNumber);
            return apRequest;
        }

        private Authenticator CreateAuthenticator(KerberosTicket ticket, AuthorizationData data, EncryptionKey subkey)
        {
            Authenticator plaintextAuthenticator = new Authenticator();
            plaintextAuthenticator.authenticator_vno = new Asn1Integer(KerberosConstValue.KERBEROSV5);
            plaintextAuthenticator.crealm = this.Context.CName.Realm;
            plaintextAuthenticator.cusec = new Microseconds(DateTime.UtcNow.Millisecond);
            plaintextAuthenticator.ctime = KerberosUtility.CurrentKerberosTime;
            plaintextAuthenticator.seq_number = new KerbUInt32(0);
            plaintextAuthenticator.cname = ticket.TicketOwner;
            plaintextAuthenticator.subkey = subkey;
            plaintextAuthenticator.authorization_data = data;
            return plaintextAuthenticator;
        }

        private Authenticator CreateAuthenticator(KerberosTicket ticket, AuthorizationData data, EncryptionKey subKey, ChecksumType checksumType, byte[] checksumBody)
        {
            Authenticator plaintextAuthenticator = CreateAuthenticator(ticket, data, subKey);

            byte[] checkData = KerberosUtility.GetChecksum(ticket.SessionKey.keyvalue.ByteArrayValue, checksumBody, (int)KeyUsageNumber.TGS_REQ_PA_TGS_REQ_adataOR_AP_REQ_Authenticator_cksum, checksumType);
            plaintextAuthenticator.cksum = new Checksum(new KerbInt32((int)checksumType), new Asn1OctetString(checkData));

            return plaintextAuthenticator;
        }

        /// <summary>
        /// Create authenticator for ChecksumType.ap_authenticator_8003
        /// </summary>
        private Authenticator CreateAuthenticator(KerberosTicket ticket, AuthorizationData data, EncryptionKey subkey, ChecksumFlags checksumFlag)
        {
            Authenticator plaintextAuthenticator = CreateAuthenticator(ticket, data, subkey);

            AuthCheckSum checksum = new AuthCheckSum();
            checksum.Lgth = KerberosConstValue.AUTHENTICATOR_CHECKSUM_LENGTH;
            checksum.Bnd = new byte[checksum.Lgth];
            checksum.Flags = (int)checksumFlag;
            byte[] checkData = ArrayUtility.ConcatenateArrays(BitConverter.GetBytes(checksum.Lgth),
                checksum.Bnd, BitConverter.GetBytes(checksum.Flags));

            plaintextAuthenticator.cksum = new Checksum(new KerbInt32((int)ChecksumType.ap_authenticator_8003), new Asn1OctetString(checkData));
            return plaintextAuthenticator;
        }

        private void UpdateDefaultSettings()
        {
            EncryptionType[] encryptionTypes = new EncryptionType[]
            {
                EncryptionType.AES256_CTS_HMAC_SHA1_96,
                EncryptionType.AES128_CTS_HMAC_SHA1_96,
                EncryptionType.RC4_HMAC,
                EncryptionType.RC4_HMAC_EXP,
                EncryptionType.UnusedValue_135,
                EncryptionType.DES_CBC_MD5,
            };

            KerbInt32[] etypes = new KerbInt32[encryptionTypes.Length];
            for (int i = 0; i < encryptionTypes.Length; i++)
            {
                etypes[i] = new KerbInt32((int)encryptionTypes[i]);
            }
            Asn1SequenceOf<KerbInt32> etype = new Asn1SequenceOf<KerbInt32>(etypes);

            Context.SupportedEType = etype;

            this.Context.Pvno = KerberosConstValue.KERBEROSV5;

            contextSizes = new SecurityPackageContextSizes();
            contextSizes.MaxTokenSize = KerberosConstValue.MAX_TOKEN_SIZE;
            contextSizes.MaxSignatureSize = KerberosConstValue.MAX_SIGNATURE_SIZE;
            contextSizes.BlockSize = KerberosConstValue.BLOCK_SIZE;
            contextSizes.SecurityTrailerSize = KerberosConstValue.SECURITY_TRAILER_SIZE;
        }

        /// <summary>
        /// Get the ApOptions flag and Checksum flag by the context attribute
        /// </summary>
        /// <param name="apOption">The apOptions flag</param>
        /// <param name="checksumFlags">The checksum flag</param>
        private void GetFlagsByContextAttribute(out ApOptions apOptions)
        {
            apOptions = ApOptions.MutualRequired;
            var checksumFlags = ChecksumFlags.None;

            if ((contextAttribute & ClientSecurityContextAttribute.Delegate) == ClientSecurityContextAttribute.Delegate)
            {
                checksumFlags |= ChecksumFlags.GSS_C_DELEG_FLAG;
            }
            if ((contextAttribute & ClientSecurityContextAttribute.MutualAuth)
                == ClientSecurityContextAttribute.MutualAuth)
            {
                checksumFlags |= ChecksumFlags.GSS_C_MUTUAL_FLAG;
                apOptions |= ApOptions.MutualRequired;
            }
            if ((contextAttribute & ClientSecurityContextAttribute.ReplayDetect)
                == ClientSecurityContextAttribute.ReplayDetect)
            {
                checksumFlags |= ChecksumFlags.GSS_C_REPLAY_FLAG;
            }
            if ((contextAttribute & ClientSecurityContextAttribute.SequenceDetect)
                == ClientSecurityContextAttribute.SequenceDetect)
            {
                checksumFlags |= ChecksumFlags.GSS_C_SEQUENCE_FLAG;
            }
            if ((contextAttribute & ClientSecurityContextAttribute.Confidentiality)
                == ClientSecurityContextAttribute.Confidentiality)
            {
                apOptions = ApOptions.None;
                checksumFlags |= ChecksumFlags.GSS_C_CONF_FLAG;
            }
            if ((contextAttribute & ClientSecurityContextAttribute.DceStyle) == ClientSecurityContextAttribute.DceStyle)
            {
                checksumFlags |= ChecksumFlags.GSS_C_DCE_STYLE;
            }
            if ((contextAttribute & ClientSecurityContextAttribute.ExtendedError)
                == ClientSecurityContextAttribute.ExtendedError)
            {
                checksumFlags |= ChecksumFlags.GSS_C_EXTENDED_ERROR_FLAG;
            }
            if ((contextAttribute & ClientSecurityContextAttribute.Integrity)
                == ClientSecurityContextAttribute.Integrity)
            {
                checksumFlags |= ChecksumFlags.GSS_C_INTEG_FLAG;
            }
            if ((contextAttribute & ClientSecurityContextAttribute.Identify) == ClientSecurityContextAttribute.Identify)
            {
                checksumFlags |= ChecksumFlags.GSS_C_IDENTIFY_FLAG;
            }

            this.Context.ChecksumFlag = checksumFlags;
        }

        private void UpdateTGTCachedToken(KerberosTicket tgtToken)
        {
            cacheLock.EnterWriteLock();
            try
            {
                CacheTokenKey findedKey = null;
                int address = this.credential.GetHashCode();
                foreach (var kvp in TGTTokenCache)
                {
                    if (kvp.Key.CredentialAddress.Equals(address) && kvp.Key.ServerPrincipleName.Equals(this.serverName))
                    {
                        findedKey = kvp.Key;
                        break;
                    }
                }
                if (findedKey != null)
                {
                    TGTTokenCache[findedKey] = tgtToken;
                }
                else
                {
                    TGTTokenCache.Add(new CacheTokenKey() { CredentialAddress = address, ServerPrincipleName = serverName }, tgtToken);
                }
            }
            finally
            {
                cacheLock.ExitWriteLock();
            }
        }

        private KerberosTicket GetTGTCachedToken(AccountCredential inputCredential, string inputServerPrincipleName)
        {
            EventSource.Log.ClientSecurityContext_GetTGTCachedTokenStarted(inputCredential, inputServerPrincipleName);

            cacheLock.EnterReadLock();
            KerberosTicket cachedTGTToken = null;

            CacheTokenKey findedKey = null;
            foreach (var kvp in TGTTokenCache)
            {
                int address = this.credential.GetHashCode();
                if (kvp.Key.CredentialAddress.Equals(inputCredential) && kvp.Key.ServerPrincipleName.Equals(inputServerPrincipleName))
                {
                    findedKey = kvp.Key;
                    break;
                }
            }
            if (findedKey != null)
            {
                cachedTGTToken = TGTTokenCache[findedKey];
                //TODO: Remove from cache if token expired
            }
            cacheLock.ExitReadLock();

            return cachedTGTToken;
        }
        #endregion

        internal class CacheTokenKey
        {
            public int CredentialAddress { get; set; }

            public string ServerPrincipleName { get; set; }
        }
    }
}
