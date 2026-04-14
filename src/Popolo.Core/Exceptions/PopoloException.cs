using System;

namespace Popolo.Core.Exceptions
{

  #region 引数異常の例外クラス定義

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

  #endregion

  #region 引数の範囲異常の例外クラス

  /// <summary>
  /// Exception thrown when a parameter value is outside its physically or
  /// mathematically valid range.
  /// </summary>
  /// <remarks>
  /// Use this exception when a value violates a known physical or mathematical
  /// bound (e.g., temperature below absolute zero, negative pressure).
  /// For argument errors unrelated to range (e.g., null, wrong array length),
  /// use <see cref="PopoloArgumentException"/> instead.
  /// </remarks>
  public class PopoloOutOfRangeException : ArgumentOutOfRangeException
  {
    /// <summary>Gets the minimum allowed value, or null if there is no lower bound.</summary>
    public double? Minimum { get; }

    /// <summary>Gets the maximum allowed value, or null if there is no upper bound.</summary>
    public double? Maximum { get; }

    /// <summary>
    /// Initializes a new instance with the parameter name, actual value,
    /// and optional minimum/maximum bounds.
    /// </summary>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    /// <param name="actualValue">The actual value that was out of range.</param>
    /// <param name="minimum">The minimum allowed value, or null if unbounded below.</param>
    /// <param name="maximum">The maximum allowed value, or null if unbounded above.</param>
    public PopoloOutOfRangeException(
        string paramName,
        double actualValue,
        double? minimum = null,
        double? maximum = null)
        : base(paramName, actualValue, BuildMessage(paramName, actualValue, minimum, maximum))
    {
      Minimum = minimum;
      Maximum = maximum;
    }

    /// <summary>
    /// Initializes a new instance with an additional custom message prefix.
    /// </summary>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    /// <param name="actualValue">The actual value that was out of range.</param>
    /// <param name="minimum">The minimum allowed value, or null if unbounded below.</param>
    /// <param name="maximum">The maximum allowed value, or null if unbounded above.</param>
    /// <param name="messagePrefix">Additional context to prepend to the message.</param>
    public PopoloOutOfRangeException(
        string paramName,
        double actualValue,
        double? minimum,
        double? maximum,
        string messagePrefix)
        : base(paramName, actualValue,
              messagePrefix + " " + BuildMessage(paramName, actualValue, minimum, maximum))
    {
      Minimum = minimum;
      Maximum = maximum;
    }

    private static string BuildMessage(
        string paramName, double actualValue, double? minimum, double? maximum)
    {
      string range = (minimum, maximum) switch
      {
        (double min, double max) => $"[{min}, {max}]",
        (double min, null) => $"[{min}, ∞)",
        (null, double max) => $"(-∞, {max}]",
        _ => "(unbounded)"
      };
      return $"Parameter '{paramName}' is out of valid range {range}. Got: {actualValue}.";
    }
  }

  #endregion

  #region 数値計算の例外クラス

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

  #endregion

  #region オブジェクト状態不正の例外クラス

  /// <summary>
  /// Thrown when a method call is invalid for the current state of the object.
  /// Typically indicates that a required property or configuration has not been
  /// set before performing an operation.
  /// </summary>
  /// <remarks>
  /// Use this exception when the error is not caused by an argument passed to
  /// the method, but by the object not being in a valid state for the operation.
  /// Common examples:
  /// <list type="bullet">
  ///   <item><description>Calling <c>Solve()</c> before setting a required reference node.</description></item>
  ///   <item><description>Calling an update method before initialization is complete.</description></item>
  /// </list>
  /// For invalid argument values, use <see cref="PopoloArgumentException"/> instead.
  /// </remarks>
  public class PopoloInvalidOperationException : InvalidOperationException
  {
    /// <summary>Gets the name of the object or class in which the error occurred.</summary>
    public string? ObjectName { get; }

    /// <summary>Gets the name of the property that was not set, if applicable.</summary>
    public string? PropertyName { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="PopoloInvalidOperationException"/>
    /// with a descriptive message.
    /// </summary>
    /// <param name="message">The error message describing why the operation is invalid.</param>
    public PopoloInvalidOperationException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance of <see cref="PopoloInvalidOperationException"/>
    /// indicating that a required property has not been set.
    /// </summary>
    /// <param name="objectName">The name of the object or class.</param>
    /// <param name="propertyName">The name of the property that must be set before this operation.</param>
    public PopoloInvalidOperationException(string objectName, string propertyName)
        : base($"{objectName}.{propertyName} must be set before performing this operation.")
    {
      ObjectName = objectName;
      PropertyName = propertyName;
    }
  }

  #endregion

  #region 未実装例外クラス

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

  #endregion

}
