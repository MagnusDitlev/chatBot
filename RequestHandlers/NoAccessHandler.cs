using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FairControlApi;
using FairControlApi.Exception;
using FairFit_DialogFlow_Fullfilmment;
using Google.Api.Gax.Grpc;
using Google.Cloud.Dialogflow.V2;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Hosting;
using static FairControlApi.Response.Member.CreatePinResponse;

public class NoAccessHandler
{
    private IWebHostEnvironment _env;
    private FairControlApiClient _api;

    private static Dictionary<string, SessionData> Sessions = new Dictionary<string, SessionData>();

    public NoAccessHandler(IWebHostEnvironment env, FairControlApiClient api)
    {
        _env = env;
        _api = api;
    }

    public async Task<WebhookResponse> HandleEnterPhone (WebhookRequest request)
    {
        var phoneNumber = request.QueryResult.Parameters.Fields["PhoneNumber"].StringValue;
        Sessions[request.Session] = new SessionData { PhoneNumber = phoneNumber };
        var response = new WebhookResponse();

        try {
            await _api.GeneratePhoneVerificationMember(phoneNumber);
            response.FollowupEventInput = new EventInput {
                Name = "NoAccess-SmsCodeSent",
                Parameters = GetParameters( new Dictionary<string, string> { { "PhoneNumber", phoneNumber }}),
            };
        }
        catch (MemberException e)
        {
            if (e.Type == FairControlExceptionType.MemberNotFound)
            {
                response.FollowupEventInput = new EventInput {
                    Name = "NoAccess-Phone-MemberNotFound",
                    Parameters = GetParameters( new Dictionary<string, string> { { "PhoneNumber", phoneNumber }}),
                };
            }
            else
            {
                throw e;
            }
        }

        return response;
    }

    public async Task<WebhookResponse> HandleEnterPhoneCode (WebhookRequest request)
    {
        var phoneNumber = Sessions[request.Session].PhoneNumber;
        var phoneToken = request.QueryResult.Parameters.Fields["SmsCode"].StringValue;
        Sessions[request.Session].PhoneToken = phoneToken;
        var response = new WebhookResponse();

        try {
            var result = await _api.ListByPhone(phoneNumber, phoneToken);
            if (result.Members.Count == 1)
            {
                // Jump to code creation
                response.FollowupEventInput = new EventInput {
                    Name = "NoAccess-Pin",
                    Parameters = GetParameters( new Dictionary<string, string> { { "SubscriptionNumber", result.Members[0].SubscriptionNumber }}),
                };
            }
            else
            {
                var outputText = "Jeg kan se at der er flere medlemskaber tilknyttet dette telefonnummer. Følgende medlemskaber er tilknyttet: ";
                foreach (var member in result.Members)
                {
                    outputText += $"{member.Name} med medlemsnummeret {member.SubscriptionNumber}. ";
                }
                outputText += "Indtast venligst dit medlemsnummer.";
                response.FulfillmentText = outputText;
            }
        }
        catch (TokenException e)
        {
            if (e.Type == FairControlExceptionType.TokenExpired || e.Type == FairControlExceptionType.TokenNotFound)
            {
                response.FollowupEventInput = new EventInput {
                    Name = "NoAccess-Phone-SmsCodeWrong",
                    Parameters = GetParameters( new Dictionary<string, string> { { "PhoneNumber", phoneNumber }}),
                };
            }
            else
            {
                throw e;
            }
        }

        return response;
    }

    public async Task<WebhookResponse> HandleCreatePin (WebhookRequest request)
    {
        var subscriptionNumber = request.QueryResult.Parameters.Fields["SubscriptionNumber"].StringValue;
        var phoneToken = Sessions[request.Session].PhoneToken;
        var phoneNumber = Sessions[request.Session].PhoneNumber;
        var response = new WebhookResponse();

        try {
            var result = await _api.CreateAccessPin(subscriptionNumber, phoneToken);
            switch (result.Result)
            {
                case CreatePinResultType.PinCreated:
                    response.FollowupEventInput = new EventInput {
                        Name = "NoAccess-Pin-Created",
                        Parameters = GetParameters( new Dictionary<string, string> { { "PinCode", result.PinCode }}),
                    };
                    break;
                case CreatePinResultType.Debt:
                    response.FollowupEventInput = new EventInput {
                        Name = "NoAccess-Pin-Debt",
                        Parameters = GetParameters( new Dictionary<string, string> {
                            { "OwedAmount", result.OwedAmount },
                            { "UpdateCardUrl", result.UpdateCardUrl }
                        }),
                    };
                    break;
                case CreatePinResultType.CardMissing:
                    response.FollowupEventInput = new EventInput {
                        Name = "NoAccess-Pin-MissingCard",
                        Parameters = GetParameters( new Dictionary<string, string> {
                            { "UpdateCardUrl", result.UpdateCardUrl }
                        }),
                    };
                    break;
                case CreatePinResultType.BioCam:
                    response.FollowupEventInput = new EventInput {
                        Name = "NoAccess-Pin-BioCam",
                    };
                    break;
                case CreatePinResultType.TooManyPins:
                    response.FollowupEventInput = new EventInput {
                        Name = "NoAccess-Pin-TooMany",
                    };
                    break;
            }
        }
        catch (TokenException e)
        {
            if (e.Type == FairControlExceptionType.TokenExpired || e.Type == FairControlExceptionType.TokenNotFound)
            {
                //response.OutputContexts.Clear();
                response.FollowupEventInput = new EventInput {
                    Name = "NoAccess-Phone-SmsCodeWrong",
                    Parameters = GetParameters( new Dictionary<string, string> { { "PhoneNumber", phoneNumber }}),
                };
            }
            else
            {
                throw e;
            }
        }

        return response;
    }

    private Struct GetParameters (Dictionary<string, string> pars)
    {
        var myStruct = new Struct();
        foreach (var par in pars)
        {
            myStruct.Fields.Add(par.Key, new Value{ StringValue = par.Value });
        }

        return myStruct;
    }

    private class SessionData
    {
        public string PhoneNumber;
        public string PhoneToken;
    }
}