using Popolo.Core.Exceptions;

namespace Popolo.Core.Tests.Exceptions
{
  public class PopoloExceptionTests
  {
    // ---- PopoloArgumentException ----

    [Fact]
    public void PopoloArgumentException_StoresMessage()
    {
      // Arrange
      var message = "Value must be positive.";
      var paramName = "temperature";

      // Act
      var ex = new PopoloArgumentException(message, paramName);

      // Assert
      Assert.Contains(message, ex.Message);
    }

    [Fact]
    public void PopoloArgumentException_StoresParamName()
    {
      // Arrange
      var paramName = "temperature";

      // Act
      var ex = new PopoloArgumentException("some message", paramName);

      // Assert
      Assert.Equal(paramName, ex.ParamName);
    }

    [Fact]
    public void PopoloArgumentException_IsArgumentException()
    {
      // Act
      var ex = new PopoloArgumentException("msg", "param");

      // Assert
      Assert.IsAssignableFrom<ArgumentException>(ex);
    }

    // ---- PopoloNumericalException ----

    [Fact]
    public void PopoloNumericalException_StoresSolverName()
    {
      // Arrange
      var solverName = "NewtonRaphson";

      // Act
      var ex = new PopoloNumericalException(solverName, "did not converge.");

      // Assert
      Assert.Equal(solverName, ex.SolverName);
    }

    [Fact]
    public void PopoloNumericalException_MessageContainsSolverName()
    {
      // Arrange
      var solverName = "NewtonRaphson";

      // Act
      var ex = new PopoloNumericalException(solverName, "did not converge.");

      // Assert
      Assert.Contains(solverName, ex.Message);
    }

    [Fact]
    public void PopoloNumericalException_PreservesInnerException()
    {
      // Arrange
      var inner = new InvalidOperationException("original error");

      // Act
      var ex = new PopoloNumericalException("Bisection", "iteration limit exceeded.", inner);

      // Assert
      Assert.Equal(inner, ex.InnerException);
    }

    [Fact]
    public void PopoloNumericalException_WithoutInnerException_InnerExceptionIsNull()
    {
      // Act
      var ex = new PopoloNumericalException("Bisection", "iteration limit exceeded.");

      // Assert
      Assert.Null(ex.InnerException);
    }

    // ---- PopoloNotImplementedException ----

    [Fact]
    public void PopoloNotImplementedException_MessageContainsFeatureName()
    {
      // Arrange
      var feature = "AdsorptionChiller.HeatCapacity";

      // Act
      var ex = new PopoloNotImplementedException(feature);

      // Assert
      Assert.Contains(feature, ex.Message);
    }

    [Fact]
    public void PopoloNotImplementedException_IsNotImplementedException()
    {
      // Act
      var ex = new PopoloNotImplementedException("some feature");

      // Assert
      Assert.IsAssignableFrom<NotImplementedException>(ex);
    }
  }
}
