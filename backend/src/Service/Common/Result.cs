namespace Ats.Service.Common;

using System;
using System.Collections.Generic;

public enum ResultStatus
{
    Success,
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    Error
}

public class Result
{
    public ResultStatus Status { get; protected set; }
    public bool IsSuccess => Status == ResultStatus.Success;
    public string? ErrorCode { get; protected set; }
    public string? ErrorMessage { get; protected set; }
    public IDictionary<string, string[]>? ValidationErrors { get; protected set; }

    public static Result Ok() => new() { Status = ResultStatus.Success };

    public static Result Validation(IDictionary<string, string[]> errors, string message = "Validation failed.") =>
        new()
        {
            Status = ResultStatus.Validation,
            ErrorMessage = message,
            ValidationErrors = errors
        };

    public static Result Validation(IDictionary<string, string[]> errors, string code, string message) =>
        new()
        {
            Status = ResultStatus.Validation,
            ErrorCode = code,
            ErrorMessage = message,
            ValidationErrors = errors
        };

    public static Result Unauthorized(string code, string message) =>
        new()
        {
            Status = ResultStatus.Unauthorized,
            ErrorCode = code,
            ErrorMessage = message
        };

    public static Result Forbidden(string code, string message) =>
        new()
        {
            Status = ResultStatus.Forbidden,
            ErrorCode = code,
            ErrorMessage = message
        };

    public static Result NotFound(string code, string message) =>
        new()
        {
            Status = ResultStatus.NotFound,
            ErrorCode = code,
            ErrorMessage = message
        };

    public static Result Conflict(string code, string message) =>
        new()
        {
            Status = ResultStatus.Conflict,
            ErrorCode = code,
            ErrorMessage = message
        };

    public static Result Error(string code, string message) =>
        new()
        {
            Status = ResultStatus.Error,
            ErrorCode = code,
            ErrorMessage = message
        };
}

public class Result<T> : Result
{
    public T? Value { get; private set; }

    public static Result<T> Ok(T value) => new() { Status = ResultStatus.Success, Value = value };

    public static new Result<T> Validation(IDictionary<string, string[]> errors, string message = "Validation failed.") =>
        new()
        {
            Status = ResultStatus.Validation,
            ErrorMessage = message,
            ValidationErrors = errors
        };

    public static new Result<T> Validation(IDictionary<string, string[]> errors, string code, string message) =>
        new()
        {
            Status = ResultStatus.Validation,
            ErrorCode = code,
            ErrorMessage = message,
            ValidationErrors = errors
        };

    public static new Result<T> Unauthorized(string code, string message) =>
        new()
        {
            Status = ResultStatus.Unauthorized,
            ErrorCode = code,
            ErrorMessage = message
        };

    public static new Result<T> Forbidden(string code, string message) =>
        new()
        {
            Status = ResultStatus.Forbidden,
            ErrorCode = code,
            ErrorMessage = message
        };

    public static new Result<T> NotFound(string code, string message) =>
        new()
        {
            Status = ResultStatus.NotFound,
            ErrorCode = code,
            ErrorMessage = message
        };

    public static new Result<T> Conflict(string code, string message) =>
        new()
        {
            Status = ResultStatus.Conflict,
            ErrorCode = code,
            ErrorMessage = message
        };

    public static new Result<T> Error(string code, string message) =>
        new()
        {
            Status = ResultStatus.Error,
            ErrorCode = code,
            ErrorMessage = message
        };
}
