// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Protocols.TestSuites.ActiveDirectory.Adts.Ldap
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Reflection;
    using Microsoft.Protocols.TestSuites.ActiveDirectory.Common;
    using Microsoft.Protocols.TestTools;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Protocols.TestTools.Messages.Runtime;

    [TestClassAttribute()]
    public partial class TestScenarioExtendedControlsSearch3Win2K8R2 : PtfTestClassBase
    {

        public TestScenarioExtendedControlsSearch3Win2K8R2()
        {
            this.SetSwitch("ProceedControlTimeout", "100");
            this.SetSwitch("QuiescenceTimeout", "30000");
        }

        #region Expect Delegates
        public delegate void SearchOpResponseDelegate1(SearchResp response);
        #endregion

        #region Event Metadata
        static System.Reflection.EventInfo SearchOpResponseInfo = TestManagerHelpers.GetEventInfo(typeof(IAD_LDAPModelAdapter), "SearchOpResponse");
        #endregion

        #region Adapter Instances
        private IAD_LDAPModelAdapter IAD_LDAPModelAdapterInstance;
        #endregion

        #region Class Initialization and Cleanup
        [ClassInitializeAttribute()]
        public static void ClassInitialize(TestContext context)
        {
            PtfTestClassBase.Initialize(context);
        }

        [ClassCleanupAttribute()]
        public static void ClassCleanup()
        {
            PtfTestClassBase.Cleanup();
        }
        #endregion

        #region Test Initialization and Cleanup
        protected override void TestInitialize()
        {
            this.InitializeTestManager();
            this.IAD_LDAPModelAdapterInstance = ((IAD_LDAPModelAdapter)(this.GetAdapter(typeof(IAD_LDAPModelAdapter))));
            this.IAD_LDAPModelAdapterInstance.SearchOpResponse += IAD_LDAPModelAdapterInstance_SearchOpResponse;
        }
        private void IAD_LDAPModelAdapterInstance_SearchOpResponse(SearchResp response)
        {
            this.Manager.AddEvent(SearchOpResponseInfo, this.IAD_LDAPModelAdapterInstance, new object[] { response });
        }
        protected override void TestCleanup()
        {
            this.IAD_LDAPModelAdapterInstance.SearchOpResponse -= IAD_LDAPModelAdapterInstance_SearchOpResponse;
            base.TestCleanup();
            this.CleanupTestManager();
        }
        #endregion

        #region Test Starting in S0
        [TestMethodAttribute()]
        [TestCategory("MS-ADTS-LDAP")]
        [TestCategory("PDC")]
        [TestCategory("DomainWin2008R2")]
        [TestCategory("ForestWin2008R2")]
        [TestCategory("Main")]
        public void LDAP_TestScenarioExtendedControlsSearch3Win2K8R2S0()
        {
            this.Manager.BeginTest("TestScenarioExtendedControlsSearch3Win2K8R2S0");
            this.Manager.Comment("reaching state \'S0\'");
            this.Manager.Comment("executing step \'call Initialize()\'");
            this.IAD_LDAPModelAdapterInstance.Initialize();
            this.Manager.Comment("reaching state \'S1\'");
            this.Manager.Comment("checking step \'return Initialize\'");
            this.Manager.Comment("reaching state \'S6\'");
            this.Manager.Comment(@"executing step 'call SearchOpReq(""CN=Administrators,CN=Builtin,DC=adts88"",""(member=*)"",Subtree,[""sAMAccountName"",""cn"",""ntSecurityDescriptor"",""member;range=10-0"",""msDS-QuotaUsed"",""msDS-QuotaEffective"",""member;range=1-0""],LDAP_SERVER_RANGE_OPTION_OID,AD_DS)'");
            this.IAD_LDAPModelAdapterInstance.SearchOpReq("CN=Administrators,CN=Builtin,DC=adts88", "(member=*)", SearchScope.Subtree, new List<string> { "sAMAccountName", "cn", "ntSecurityDescriptor", "member;range=10-0", "msDS-QuotaUsed", "msDS-QuotaEffective", "member;range=1-0" }, ExtendedControl.LDAP_SERVER_RANGE_OPTION_OID, ((ADImplementations)(0)));
            this.Manager.Comment("reaching state \'S9\'");
            this.Manager.Comment("checking step \'return SearchOpReq\'");
            TestScenarioExtendedControlsSearch3Win2K8R2S12();
            this.Manager.EndTest();
        }

        private void TestScenarioExtendedControlsSearch3Win2K8R2S12()
        {
            this.Manager.Comment("reaching state \'S12\'");
            int temp0 = this.Manager.ExpectEvent(this.QuiescenceTimeout, true, new ExpectedEvent(TestScenarioExtendedControlsSearch3Win2K8R2.SearchOpResponseInfo, null, new SearchOpResponseDelegate1(this.TestScenarioExtendedControlsSearch3Win2K8R2S0SearchOpResponseChecker)), new ExpectedEvent(TestScenarioExtendedControlsSearch3Win2K8R2.SearchOpResponseInfo, null, new SearchOpResponseDelegate1(this.TestScenarioExtendedControlsSearch3Win2K8R2S0SearchOpResponseChecker1)));
            if ((temp0 == 0))
            {
                this.Manager.Comment("reaching state \'S13\'");
                this.Manager.Comment("executing step \'call UnBind()\'");
                this.IAD_LDAPModelAdapterInstance.UnBind();
                this.Manager.Comment("reaching state \'S15\'");
                this.Manager.Comment("checking step \'return UnBind\'");
                this.Manager.Comment("reaching state \'S17\'");
                goto label0;
            }
            if ((temp0 == 1))
            {
                this.Manager.Comment("reaching state \'S14\'");
                this.Manager.Comment("executing step \'call UnBind()\'");
                this.IAD_LDAPModelAdapterInstance.UnBind();
                this.Manager.Comment("reaching state \'S16\'");
                this.Manager.Comment("checking step \'return UnBind\'");
                this.Manager.Comment("reaching state \'S18\'");
                goto label0;
            }
            this.Manager.CheckObservationTimeout(false, new ExpectedEvent(TestScenarioExtendedControlsSearch3Win2K8R2.SearchOpResponseInfo, null, new SearchOpResponseDelegate1(this.TestScenarioExtendedControlsSearch3Win2K8R2S0SearchOpResponseChecker)), new ExpectedEvent(TestScenarioExtendedControlsSearch3Win2K8R2.SearchOpResponseInfo, null, new SearchOpResponseDelegate1(this.TestScenarioExtendedControlsSearch3Win2K8R2S0SearchOpResponseChecker1)));
        label0:
            ;
        }

        private void TestScenarioExtendedControlsSearch3Win2K8R2S0SearchOpResponseChecker(SearchResp response)
        {
            this.Manager.Comment("checking step \'event SearchOpResponse(retreivalUnsuccessful)\'");
            TestManagerHelpers.AssertAreEqual<SearchResp>(this.Manager, ((SearchResp)(1)), response, "response of SearchOpResponse, state S12");
        }

        private void TestScenarioExtendedControlsSearch3Win2K8R2S0SearchOpResponseChecker1(SearchResp response)
        {
            this.Manager.Comment("checking step \'event SearchOpResponse(retrievalSuccessful)\'");
            TestManagerHelpers.AssertAreEqual<SearchResp>(this.Manager, ((SearchResp)(0)), response, "response of SearchOpResponse, state S12");
        }
        #endregion

        #region Test Starting in S2
        [TestMethodAttribute()]
        [TestCategory("MS-ADTS-LDAP")]
        [TestCategory("PDC")]
        [TestCategory("DomainWin2008R2")]
        [TestCategory("ForestWin2008R2")]
        [TestCategory("Main")]
        public void LDAP_TestScenarioExtendedControlsSearch3Win2K8R2S2()
        {
            this.Manager.BeginTest("TestScenarioExtendedControlsSearch3Win2K8R2S2");
            this.Manager.Comment("reaching state \'S2\'");
            this.Manager.Comment("executing step \'call Initialize()\'");
            this.IAD_LDAPModelAdapterInstance.Initialize();
            this.Manager.Comment("reaching state \'S3\'");
            this.Manager.Comment("checking step \'return Initialize\'");
            this.Manager.Comment("reaching state \'S7\'");
            this.Manager.Comment(@"executing step 'call SearchOpReq(""CN=Administrators,CN=Builtin,DC=adts88"",""(member=*)"",Subtree,[""sAMAccountName"",""cn"",""ntSecurityDescriptor"",""member;range=10-0"",""msDS-QuotaUsed"",""msDS-QuotaEffective"",""member;range=1-0""],LDAP_SERVER_RANGE_RETRIEVAL_NOERR_OID,AD_DS)'");
            this.IAD_LDAPModelAdapterInstance.SearchOpReq("CN=Administrators,CN=Builtin,DC=adts88", "(member=*)", SearchScope.Subtree, new List<string> { "sAMAccountName", "cn", "ntSecurityDescriptor", "member;range=10-0", "msDS-QuotaUsed", "msDS-QuotaEffective", "member;range=1-0" }, ExtendedControl.LDAP_SERVER_RANGE_RETRIEVAL_NOERR_OID, ((ADImplementations)(0)));
            this.Manager.Comment("reaching state \'S10\'");
            this.Manager.Comment("checking step \'return SearchOpReq\'");
            TestScenarioExtendedControlsSearch3Win2K8R2S12();
            this.Manager.EndTest();
        }
        #endregion

        #region Test Starting in S4
        [TestMethodAttribute()]
        [TestCategory("MS-ADTS-LDAP")]
        [TestCategory("PDC")]
        [TestCategory("DomainWin2008R2")]
        [TestCategory("ForestWin2008R2")]
        [TestCategory("Main")]
        public void LDAP_TestScenarioExtendedControlsSearch3Win2K8R2S4()
        {
            this.Manager.BeginTest("TestScenarioExtendedControlsSearch3Win2K8R2S4");
            this.Manager.Comment("reaching state \'S4\'");
            this.Manager.Comment("executing step \'call Initialize()\'");
            this.IAD_LDAPModelAdapterInstance.Initialize();
            this.Manager.Comment("reaching state \'S5\'");
            this.Manager.Comment("checking step \'return Initialize\'");
            this.Manager.Comment("reaching state \'S8\'");
            this.Manager.Comment(@"executing step 'call SearchOpReq(""CN=Administrators,CN=Builtin,DC=adts88"",""(objectClass=group)"",baseObject,[""sAMAccountName"",""cn"",""ntSecurityDescriptor"",""member;range=10-0"",""msDS-QuotaUsed"",""msDS-QuotaEffective"",""member;range=1-0""],LDAP_SERVER_ASQ_OID,AD_DS)'");
            this.IAD_LDAPModelAdapterInstance.SearchOpReq("CN=Administrators,CN=Builtin,DC=adts88", "(objectClass=group)", ((SearchScope)(0)), new List<string> { "sAMAccountName", "cn", "ntSecurityDescriptor", "member", "msDS-QuotaUsed", "msDS-QuotaEffective" }, ExtendedControl.LDAP_SERVER_ASQ_OID, ((ADImplementations)(0)));
            this.Manager.Comment("reaching state \'S11\'");
            this.Manager.Comment("checking step \'return SearchOpReq\'");
            TestScenarioExtendedControlsSearch3Win2K8R2S12();
            this.Manager.EndTest();
        }
        #endregion
    }
}
