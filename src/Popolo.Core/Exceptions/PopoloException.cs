using System;

namespace Popolo.Exceptions
{
  /// <summary>
  /// Thrown when an argument is physically or numerically invalid.
  /// Indicates a bug in the calling code.
  /// </summary>
  public class PopoloArgumentException : ArgumentException
  {
    /// <summary>
    /// Initializes a new instance of <see cref="PopoloArgumentException"/>.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="paramName">The name of the invalid parameter.</param>
    public PopoloArgumentException(string message, string paramName)
        : base(message, paramName) { }
  }

  /// <summary>
  /// Thrown when a numerical solver fails to converge,
  /// or encounters a singular matrix or similar numerical problem.
  /// Indicates that input values or time steps should be reviewed.
  /// </summary>
  public class PopoloNumericalException : Exception
  {
    /// <summary>Gets the name of the solver that failed.</summary>
    public string SolverName { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="PopoloNumericalException"/>.
    /// </summary>
    /// <param name="solverName">The name of the solver that failed.</param>
    /// <param name="message">The error message describing the failure.</param>
    /// <param name="inner">The exception that caused this exception, if any.</param>

    public PopoloNumericalException(string solverName, string message, Exception? inner = null)
        : base($"[{solverName}] {message}", inner)
    {
      SolverName = solverName;
    }
  }

  /// <summary>
  /// Thrown when an unimplemented code path is reached.
  /// Indicates a gap in the implementation for developers.
  /// </summary>
  public class PopoloNotImplementedException : NotImplementedException
  {
    /// <summary>
    /// Initializes a new instance of <see cref="PopoloNotImplementedException"/>.
    /// </summary>
    /// <param name="feature">The name or description of the unimplemented feature.</param>

    public PopoloNotImplementedException(string feature)
        : base($"Not implemented: {feature}") { }
  }
}
