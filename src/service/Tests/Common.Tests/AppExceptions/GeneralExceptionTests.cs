﻿using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.PS.FlightingService.Common.AppExcpetions;

namespace Microsoft.PS.FlightingService.Common.Tests.AppExceptions
{
    [ExcludeFromCodeCoverage]
    [TestClass]
    public class GeneralExceptionTests
    {
        [TestMethod]
        public void GeneralException_ShouldGetCreated()
        {
            #region Act
            var exception = new GeneralException(innerException: new Exception(),
                    correlationId: Guid.NewGuid().ToString(),
                    transactionId: Guid.NewGuid().ToString(),
                    failedMethod: "Test");
            #endregion Act

            #region Assert
            Assert.IsNotNull(exception);
            Assert.AreEqual(Constants.Exception.Types.GENERAL, exception.Type);
            Assert.AreEqual(string.Format(Constants.Exception.GeneralException.DisplayMessage, exception.CorrelationId), exception.DisplayMessage);
            #endregion Assert
        }
    }
}
