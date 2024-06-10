async Task Main()
{
	var hostUrl = "https://gbp-app-api-prod.azurewebsites.net/";

	var client = new HttpClient();
	client.BaseAddress = new Uri(hostUrl);

	var accountClient = new AccountClient(client);

	var creds = await accountClient.LoginAsync("bethaltruckstop", new UserLoginDto
	{
		Username = "george.cronje",
		Password = "Biltong@1"
	});

	//creds.Dump();

	var token = creds.Token;

	client.DefaultRequestHeaders.Add("Authorization", $"bearer {token}");

	var serviceTypeClient = new ServiceTypeClient(client);
	var serviceTypes = await serviceTypeClient.Get_allAsync();

	var rosClient = new RecordOfServiceClient(client);

	var rosList = await rosClient.Get_recent_unpaid_rosAsync(10);
	
	var detailsTasks = new List<Task<RecordOfServiceDetailedResponse>>();
	
	var downloadLock = new SemaphoreSlim(5);
	
	foreach (var ros in rosList)
	{
		var t = GetDetails(rosClient, downloadLock, ros.Id);
		detailsTasks.Add(t);
	}
	
	var detailsResult = await Task.WhenAll(detailsTasks);

	foreach (var ros in detailsResult)
	{
		var rosObj = JObject.FromObject(new 
		{
			ros.Id,
			ros.FromDate,
			ros.ToDate,
			Total = ros.Amount
		});
		var rosLinesObj = new JArray();
		
		rosObj["Lines"] = rosLinesObj;
		
		var lineItems = ros.LineItems;

		foreach (var lineItem in lineItems)
		{
			var rosLineObj = MapLineItemToJObject(lineItem, serviceTypes);
			rosLinesObj.Add(rosLineObj);
		}
		
		var uniqueServiceTypes = serviceTypes.Select(x => x.DisplayName).Distinct().OrderByDescending(x => x);
		var columns = rosLinesObj
			.First().OfType<JProperty>()
			.Select(x => x.Name)
			.Where(x => uniqueServiceTypes.Contains(x) == false)
			.Where(x => x != "Total")
			.ToList();
			
		columns.AddRange(uniqueServiceTypes);
		columns.Add("Total");

		var sb = new StringBuilder();
		var colsForOutput = columns.Select(c => $"""{c}""").ToArray();
		sb.AppendLine(string.Join(',', colsForOutput));
		foreach (var item in rosLinesObj)
		{
			var lines = new List<string>();
			foreach (var service in columns)
			{
				var outputItem = $"""{item[service] ?? 0}""";
				lines.Add(outputItem);
			}
			sb.AppendLine(string.Join(',', lines));
			
		}
		//sb.ToString().Dump();
		var fileName = $"ros_{ros.Id}.csv";
		File.WriteAllText(@$"C:\temp\RosCsv\{fileName}", sb.ToString());
		"--------------------------------------------------------".Dump();
		//rosObj.Dump();
	}
}

private async Task<RecordOfServiceDetailedResponse> GetDetails(RecordOfServiceClient rosClient, SemaphoreSlim downloadLock, int id)
{
	try
	{	        
		await downloadLock.WaitAsync();
		var result = await rosClient.Get_ros_by_idAsync(id);
		return result;
	}
	finally
	{
		downloadLock.Release();
	}
}

private JObject MapLineItemToJObject(RecordOfServiceDetailedLineItem lineItem, ICollection<GetServiceTypeListItemResponse> serviceTypes)
{
	var rosLineObj = JObject.FromObject(new
	{
		lineItem.Id,
		lineItem.ArrivalDateTime,
		lineItem.DepartureDateTime,
		lineItem.DriverName,
		lineItem.TruckRego,
		Total = lineItem.Amount
	});

	var thingy = lineItem.SubLineItems.GroupBy(x => x.ServiceTypeId).Select(x => new
	{
		ServiceName = serviceTypes.Where(t => t.Id == x.Key).Select(t => t.DisplayName).Single(),
		Amount = x.Sum(t => t.Amount)
	});
	foreach (var serviceTotal in thingy)
	{
		rosLineObj[serviceTotal.ServiceName] = serviceTotal.Amount;
	}
	
	return rosLineObj;
}

public partial class RecordOfServiceBasicResponse
{
	[Newtonsoft.Json.JsonProperty("id", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public int Id { get; set; }

	[Newtonsoft.Json.JsonProperty("fromDate", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public System.DateTimeOffset FromDate { get; set; }

	[Newtonsoft.Json.JsonProperty("toDate", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public System.DateTimeOffset ToDate { get; set; }

	[Newtonsoft.Json.JsonProperty("referenceNumber", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public string ReferenceNumber { get; set; }

	[Newtonsoft.Json.JsonProperty("comments", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public string Comments { get; set; }

	[Newtonsoft.Json.JsonProperty("discount", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public double Discount { get; set; }

	[Newtonsoft.Json.JsonProperty("amount", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public double Amount { get; set; }

	[Newtonsoft.Json.JsonProperty("client", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public RecordOfServiceClientInfo Client { get; set; }

}

[System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "13.20.0.0 (NJsonSchema v10.9.0.0 (Newtonsoft.Json v13.0.0.0))")]
public partial class RecordOfServiceClientInfo
{
	[Newtonsoft.Json.JsonProperty("id", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public int Id { get; set; }

	[Newtonsoft.Json.JsonProperty("name", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public string Name { get; set; }

	[Newtonsoft.Json.JsonProperty("clientReference", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public string ClientReference { get; set; }

}

public partial class RecordOfServiceDetailedLineItem
{
	[Newtonsoft.Json.JsonProperty("id", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public int Id { get; set; }

	[Newtonsoft.Json.JsonProperty("arrivalDateTime", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public System.DateTimeOffset? ArrivalDateTime { get; set; }

	[Newtonsoft.Json.JsonProperty("departureDateTime", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public System.DateTimeOffset? DepartureDateTime { get; set; }

	[Newtonsoft.Json.JsonProperty("driverName", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public string DriverName { get; set; }

	[Newtonsoft.Json.JsonProperty("truckRego", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public string TruckRego { get; set; }

	[Newtonsoft.Json.JsonProperty("referenceNumber", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public string ReferenceNumber { get; set; }

	[Newtonsoft.Json.JsonProperty("description", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public string Description { get; set; }

	[Newtonsoft.Json.JsonProperty("comments", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public string Comments { get; set; }

	[Newtonsoft.Json.JsonProperty("discount", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public double Discount { get; set; }

	[Newtonsoft.Json.JsonProperty("amount", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public double Amount { get; set; }

	[Newtonsoft.Json.JsonProperty("quantity", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public double Quantity { get; set; }

	[Newtonsoft.Json.JsonProperty("ignoreQtyForTotal", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public bool IgnoreQtyForTotal { get; set; }

	[Newtonsoft.Json.JsonProperty("subLineItems", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public System.Collections.Generic.ICollection<RecordOfServiceDetailedSubLineItem> SubLineItems { get; set; }

	[Newtonsoft.Json.JsonProperty("visitId", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public int? VisitId { get; set; }

}

[System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "13.20.0.0 (NJsonSchema v10.9.0.0 (Newtonsoft.Json v13.0.0.0))")]
public partial class RecordOfServiceDetailedResponse
{
	[Newtonsoft.Json.JsonProperty("id", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public int Id { get; set; }

	[Newtonsoft.Json.JsonProperty("fromDate", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public System.DateTimeOffset FromDate { get; set; }

	[Newtonsoft.Json.JsonProperty("toDate", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public System.DateTimeOffset ToDate { get; set; }

	[Newtonsoft.Json.JsonProperty("referenceNumber", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public string ReferenceNumber { get; set; }

	[Newtonsoft.Json.JsonProperty("comments", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public string Comments { get; set; }

	[Newtonsoft.Json.JsonProperty("discount", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public double Discount { get; set; }

	[Newtonsoft.Json.JsonProperty("amount", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public double Amount { get; set; }

	[Newtonsoft.Json.JsonProperty("client", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public RecordOfServiceClientInfo Client { get; set; }

	[Newtonsoft.Json.JsonProperty("lineItems", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public System.Collections.Generic.ICollection<RecordOfServiceDetailedLineItem> LineItems { get; set; }

}

[System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "13.20.0.0 (NJsonSchema v10.9.0.0 (Newtonsoft.Json v13.0.0.0))")]
public partial class RecordOfServiceDetailedSubLineItem
{
	[Newtonsoft.Json.JsonProperty("id", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public int Id { get; set; }

	[Newtonsoft.Json.JsonProperty("subReferenceNumber", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public string SubReferenceNumber { get; set; }

	[Newtonsoft.Json.JsonProperty("subDescription", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public string SubDescription { get; set; }

	[Newtonsoft.Json.JsonProperty("comments", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public string Comments { get; set; }

	[Newtonsoft.Json.JsonProperty("discount", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public double Discount { get; set; }

	[Newtonsoft.Json.JsonProperty("amount", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public double Amount { get; set; }

	[Newtonsoft.Json.JsonProperty("quantity", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public double Quantity { get; set; }

	[Newtonsoft.Json.JsonProperty("ignoreQtyForTotal", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public bool IgnoreQtyForTotal { get; set; }

	[Newtonsoft.Json.JsonProperty("visitServiceId", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public int? VisitServiceId { get; set; }

	[Newtonsoft.Json.JsonProperty("serviceTypeId", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public int? ServiceTypeId { get; set; }

}

public partial class RecordOfServiceClient
{
	private System.Net.Http.HttpClient _httpClient;
	private System.Lazy<Newtonsoft.Json.JsonSerializerSettings> _settings;

	public RecordOfServiceClient(System.Net.Http.HttpClient httpClient)
	{
		_httpClient = httpClient;
		_settings = new System.Lazy<Newtonsoft.Json.JsonSerializerSettings>(CreateSerializerSettings, true);
	}

	private Newtonsoft.Json.JsonSerializerSettings CreateSerializerSettings()
	{
		var settings = new Newtonsoft.Json.JsonSerializerSettings();
		UpdateJsonSerializerSettings(settings);
		return settings;
	}

	protected Newtonsoft.Json.JsonSerializerSettings JsonSerializerSettings { get { return _settings.Value; } }

	partial void UpdateJsonSerializerSettings(Newtonsoft.Json.JsonSerializerSettings settings);

	partial void PrepareRequest(System.Net.Http.HttpClient client, System.Net.Http.HttpRequestMessage request, string url);
	partial void PrepareRequest(System.Net.Http.HttpClient client, System.Net.Http.HttpRequestMessage request, System.Text.StringBuilder urlBuilder);
	partial void ProcessResponse(System.Net.Http.HttpClient client, System.Net.Http.HttpResponseMessage response);

	/// <returns>Success</returns>
	/// <exception cref="ApiException">A server side error occurred.</exception>
	public virtual System.Threading.Tasks.Task<System.Collections.Generic.ICollection<RecordOfServiceBasicResponse>> Get_unpaid_ros_for_clientAsync(int clientId)
	{
		return Get_unpaid_ros_for_clientAsync(clientId, System.Threading.CancellationToken.None);
	}

	/// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
	/// <returns>Success</returns>
	/// <exception cref="ApiException">A server side error occurred.</exception>
	public virtual async System.Threading.Tasks.Task<System.Collections.Generic.ICollection<RecordOfServiceBasicResponse>> Get_unpaid_ros_for_clientAsync(int clientId, System.Threading.CancellationToken cancellationToken)
	{
		if (clientId == null)
			throw new System.ArgumentNullException("clientId");

		var urlBuilder_ = new System.Text.StringBuilder();
		urlBuilder_.Append("api/RecordOfService/get_unpaid_ros_for_client/{clientId}");
		urlBuilder_.Replace("{clientId}", System.Uri.EscapeDataString(ConvertToString(clientId, System.Globalization.CultureInfo.InvariantCulture)));

		var client_ = _httpClient;
		var disposeClient_ = false;
		try
		{
			using (var request_ = new System.Net.Http.HttpRequestMessage())
			{
				request_.Method = new System.Net.Http.HttpMethod("GET");
				request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("text/plain"));

				PrepareRequest(client_, request_, urlBuilder_);

				var url_ = urlBuilder_.ToString();
				request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);

				PrepareRequest(client_, request_, url_);

				var response_ = await client_.SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
				var disposeResponse_ = true;
				try
				{
					var headers_ = System.Linq.Enumerable.ToDictionary(response_.Headers, h_ => h_.Key, h_ => h_.Value);
					if (response_.Content != null && response_.Content.Headers != null)
					{
						foreach (var item_ in response_.Content.Headers)
							headers_[item_.Key] = item_.Value;
					}

					ProcessResponse(client_, response_);

					var status_ = (int)response_.StatusCode;
					if (status_ == 200)
					{
						var objectResponse_ = await ReadObjectResponseAsync<System.Collections.Generic.ICollection<RecordOfServiceBasicResponse>>(response_, headers_, cancellationToken).ConfigureAwait(false);
						if (objectResponse_.Object == null)
						{
							throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
						}
						return objectResponse_.Object;
					}
					else
					{
						var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
						throw new ApiException("The HTTP status code of the response was not expected (" + status_ + ").", status_, responseData_, headers_, null);
					}
				}
				finally
				{
					if (disposeResponse_)
						response_.Dispose();
				}
			}
		}
		finally
		{
			if (disposeClient_)
				client_.Dispose();
		}
	}

	/// <returns>Success</returns>
	/// <exception cref="ApiException">A server side error occurred.</exception>
	public virtual System.Threading.Tasks.Task<System.Collections.Generic.ICollection<RecordOfServiceBasicResponse>> Get_partialpaid_ros_for_clientAsync(int clientId)
	{
		return Get_partialpaid_ros_for_clientAsync(clientId, System.Threading.CancellationToken.None);
	}

	/// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
	/// <returns>Success</returns>
	/// <exception cref="ApiException">A server side error occurred.</exception>
	public virtual async System.Threading.Tasks.Task<System.Collections.Generic.ICollection<RecordOfServiceBasicResponse>> Get_partialpaid_ros_for_clientAsync(int clientId, System.Threading.CancellationToken cancellationToken)
	{
		if (clientId == null)
			throw new System.ArgumentNullException("clientId");

		var urlBuilder_ = new System.Text.StringBuilder();
		urlBuilder_.Append("api/RecordOfService/get_partialpaid_ros_for_client/{clientId}");
		urlBuilder_.Replace("{clientId}", System.Uri.EscapeDataString(ConvertToString(clientId, System.Globalization.CultureInfo.InvariantCulture)));

		var client_ = _httpClient;
		var disposeClient_ = false;
		try
		{
			using (var request_ = new System.Net.Http.HttpRequestMessage())
			{
				request_.Method = new System.Net.Http.HttpMethod("GET");
				request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("text/plain"));

				PrepareRequest(client_, request_, urlBuilder_);

				var url_ = urlBuilder_.ToString();
				request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);

				PrepareRequest(client_, request_, url_);

				var response_ = await client_.SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
				var disposeResponse_ = true;
				try
				{
					var headers_ = System.Linq.Enumerable.ToDictionary(response_.Headers, h_ => h_.Key, h_ => h_.Value);
					if (response_.Content != null && response_.Content.Headers != null)
					{
						foreach (var item_ in response_.Content.Headers)
							headers_[item_.Key] = item_.Value;
					}

					ProcessResponse(client_, response_);

					var status_ = (int)response_.StatusCode;
					if (status_ == 200)
					{
						var objectResponse_ = await ReadObjectResponseAsync<System.Collections.Generic.ICollection<RecordOfServiceBasicResponse>>(response_, headers_, cancellationToken).ConfigureAwait(false);
						if (objectResponse_.Object == null)
						{
							throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
						}
						return objectResponse_.Object;
					}
					else
					{
						var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
						throw new ApiException("The HTTP status code of the response was not expected (" + status_ + ").", status_, responseData_, headers_, null);
					}
				}
				finally
				{
					if (disposeResponse_)
						response_.Dispose();
				}
			}
		}
		finally
		{
			if (disposeClient_)
				client_.Dispose();
		}
	}

	/// <returns>Success</returns>
	/// <exception cref="ApiException">A server side error occurred.</exception>
	public virtual System.Threading.Tasks.Task<System.Collections.Generic.ICollection<RecordOfServiceBasicResponse>> Get_fullypaid_ros_for_clientAsync(int clientId)
	{
		return Get_fullypaid_ros_for_clientAsync(clientId, System.Threading.CancellationToken.None);
	}

	/// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
	/// <returns>Success</returns>
	/// <exception cref="ApiException">A server side error occurred.</exception>
	public virtual async System.Threading.Tasks.Task<System.Collections.Generic.ICollection<RecordOfServiceBasicResponse>> Get_fullypaid_ros_for_clientAsync(int clientId, System.Threading.CancellationToken cancellationToken)
	{
		if (clientId == null)
			throw new System.ArgumentNullException("clientId");

		var urlBuilder_ = new System.Text.StringBuilder();
		urlBuilder_.Append("api/RecordOfService/get_fullypaid_ros_for_client/{clientId}");
		urlBuilder_.Replace("{clientId}", System.Uri.EscapeDataString(ConvertToString(clientId, System.Globalization.CultureInfo.InvariantCulture)));

		var client_ = _httpClient;
		var disposeClient_ = false;
		try
		{
			using (var request_ = new System.Net.Http.HttpRequestMessage())
			{
				request_.Method = new System.Net.Http.HttpMethod("GET");
				request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("text/plain"));

				PrepareRequest(client_, request_, urlBuilder_);

				var url_ = urlBuilder_.ToString();
				request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);

				PrepareRequest(client_, request_, url_);

				var response_ = await client_.SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
				var disposeResponse_ = true;
				try
				{
					var headers_ = System.Linq.Enumerable.ToDictionary(response_.Headers, h_ => h_.Key, h_ => h_.Value);
					if (response_.Content != null && response_.Content.Headers != null)
					{
						foreach (var item_ in response_.Content.Headers)
							headers_[item_.Key] = item_.Value;
					}

					ProcessResponse(client_, response_);

					var status_ = (int)response_.StatusCode;
					if (status_ == 200)
					{
						var objectResponse_ = await ReadObjectResponseAsync<System.Collections.Generic.ICollection<RecordOfServiceBasicResponse>>(response_, headers_, cancellationToken).ConfigureAwait(false);
						if (objectResponse_.Object == null)
						{
							throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
						}
						return objectResponse_.Object;
					}
					else
					{
						var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
						throw new ApiException("The HTTP status code of the response was not expected (" + status_ + ").", status_, responseData_, headers_, null);
					}
				}
				finally
				{
					if (disposeResponse_)
						response_.Dispose();
				}
			}
		}
		finally
		{
			if (disposeClient_)
				client_.Dispose();
		}
	}

	/// <returns>Success</returns>
	/// <exception cref="ApiException">A server side error occurred.</exception>
	public virtual System.Threading.Tasks.Task<System.Collections.Generic.ICollection<RecordOfServiceBasicResponse>> Get_recent_unpaid_rosAsync(int count)
	{
		return Get_recent_unpaid_rosAsync(count, System.Threading.CancellationToken.None);
	}

	/// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
	/// <returns>Success</returns>
	/// <exception cref="ApiException">A server side error occurred.</exception>
	public virtual async System.Threading.Tasks.Task<System.Collections.Generic.ICollection<RecordOfServiceBasicResponse>> Get_recent_unpaid_rosAsync(int count, System.Threading.CancellationToken cancellationToken)
	{
		if (count == null)
			throw new System.ArgumentNullException("count");

		var urlBuilder_ = new System.Text.StringBuilder();
		urlBuilder_.Append("api/RecordOfService/get_recent_unpaid_ros/{count}");
		urlBuilder_.Replace("{count}", System.Uri.EscapeDataString(ConvertToString(count, System.Globalization.CultureInfo.InvariantCulture)));

		var client_ = _httpClient;
		var disposeClient_ = false;
		try
		{
			using (var request_ = new System.Net.Http.HttpRequestMessage())
			{
				request_.Method = new System.Net.Http.HttpMethod("GET");
				request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("text/plain"));

				PrepareRequest(client_, request_, urlBuilder_);

				var url_ = urlBuilder_.ToString();
				request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);

				PrepareRequest(client_, request_, url_);

				var response_ = await client_.SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
				var disposeResponse_ = true;
				try
				{
					var headers_ = System.Linq.Enumerable.ToDictionary(response_.Headers, h_ => h_.Key, h_ => h_.Value);
					if (response_.Content != null && response_.Content.Headers != null)
					{
						foreach (var item_ in response_.Content.Headers)
							headers_[item_.Key] = item_.Value;
					}

					ProcessResponse(client_, response_);

					var status_ = (int)response_.StatusCode;
					if (status_ == 200)
					{
						var objectResponse_ = await ReadObjectResponseAsync<System.Collections.Generic.ICollection<RecordOfServiceBasicResponse>>(response_, headers_, cancellationToken).ConfigureAwait(false);
						if (objectResponse_.Object == null)
						{
							throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
						}
						return objectResponse_.Object;
					}
					else
					{
						var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
						throw new ApiException("The HTTP status code of the response was not expected (" + status_ + ").", status_, responseData_, headers_, null);
					}
				}
				finally
				{
					if (disposeResponse_)
						response_.Dispose();
				}
			}
		}
		finally
		{
			if (disposeClient_)
				client_.Dispose();
		}
	}

	/// <returns>Success</returns>
	/// <exception cref="ApiException">A server side error occurred.</exception>
	public virtual System.Threading.Tasks.Task<System.Collections.Generic.ICollection<RecordOfServiceBasicResponse>> Get_recent_partialpaid_rosAsync(int count)
	{
		return Get_recent_partialpaid_rosAsync(count, System.Threading.CancellationToken.None);
	}

	/// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
	/// <returns>Success</returns>
	/// <exception cref="ApiException">A server side error occurred.</exception>
	public virtual async System.Threading.Tasks.Task<System.Collections.Generic.ICollection<RecordOfServiceBasicResponse>> Get_recent_partialpaid_rosAsync(int count, System.Threading.CancellationToken cancellationToken)
	{
		if (count == null)
			throw new System.ArgumentNullException("count");

		var urlBuilder_ = new System.Text.StringBuilder();
		urlBuilder_.Append("api/RecordOfService/get_recent_partialpaid_ros/{count}");
		urlBuilder_.Replace("{count}", System.Uri.EscapeDataString(ConvertToString(count, System.Globalization.CultureInfo.InvariantCulture)));

		var client_ = _httpClient;
		var disposeClient_ = false;
		try
		{
			using (var request_ = new System.Net.Http.HttpRequestMessage())
			{
				request_.Method = new System.Net.Http.HttpMethod("GET");
				request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("text/plain"));

				PrepareRequest(client_, request_, urlBuilder_);

				var url_ = urlBuilder_.ToString();
				request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);

				PrepareRequest(client_, request_, url_);

				var response_ = await client_.SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
				var disposeResponse_ = true;
				try
				{
					var headers_ = System.Linq.Enumerable.ToDictionary(response_.Headers, h_ => h_.Key, h_ => h_.Value);
					if (response_.Content != null && response_.Content.Headers != null)
					{
						foreach (var item_ in response_.Content.Headers)
							headers_[item_.Key] = item_.Value;
					}

					ProcessResponse(client_, response_);

					var status_ = (int)response_.StatusCode;
					if (status_ == 200)
					{
						var objectResponse_ = await ReadObjectResponseAsync<System.Collections.Generic.ICollection<RecordOfServiceBasicResponse>>(response_, headers_, cancellationToken).ConfigureAwait(false);
						if (objectResponse_.Object == null)
						{
							throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
						}
						return objectResponse_.Object;
					}
					else
					{
						var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
						throw new ApiException("The HTTP status code of the response was not expected (" + status_ + ").", status_, responseData_, headers_, null);
					}
				}
				finally
				{
					if (disposeResponse_)
						response_.Dispose();
				}
			}
		}
		finally
		{
			if (disposeClient_)
				client_.Dispose();
		}
	}

	/// <returns>Success</returns>
	/// <exception cref="ApiException">A server side error occurred.</exception>
	public virtual System.Threading.Tasks.Task<System.Collections.Generic.ICollection<RecordOfServiceBasicResponse>> Get_recent_fullypaid_rosAsync(int count)
	{
		return Get_recent_fullypaid_rosAsync(count, System.Threading.CancellationToken.None);
	}

	/// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
	/// <returns>Success</returns>
	/// <exception cref="ApiException">A server side error occurred.</exception>
	public virtual async System.Threading.Tasks.Task<System.Collections.Generic.ICollection<RecordOfServiceBasicResponse>> Get_recent_fullypaid_rosAsync(int count, System.Threading.CancellationToken cancellationToken)
	{
		if (count == null)
			throw new System.ArgumentNullException("count");

		var urlBuilder_ = new System.Text.StringBuilder();
		urlBuilder_.Append("api/RecordOfService/get_recent_fullypaid_ros/{count}");
		urlBuilder_.Replace("{count}", System.Uri.EscapeDataString(ConvertToString(count, System.Globalization.CultureInfo.InvariantCulture)));

		var client_ = _httpClient;
		var disposeClient_ = false;
		try
		{
			using (var request_ = new System.Net.Http.HttpRequestMessage())
			{
				request_.Method = new System.Net.Http.HttpMethod("GET");
				request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("text/plain"));

				PrepareRequest(client_, request_, urlBuilder_);

				var url_ = urlBuilder_.ToString();
				request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);

				PrepareRequest(client_, request_, url_);

				var response_ = await client_.SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
				var disposeResponse_ = true;
				try
				{
					var headers_ = System.Linq.Enumerable.ToDictionary(response_.Headers, h_ => h_.Key, h_ => h_.Value);
					if (response_.Content != null && response_.Content.Headers != null)
					{
						foreach (var item_ in response_.Content.Headers)
							headers_[item_.Key] = item_.Value;
					}

					ProcessResponse(client_, response_);

					var status_ = (int)response_.StatusCode;
					if (status_ == 200)
					{
						var objectResponse_ = await ReadObjectResponseAsync<System.Collections.Generic.ICollection<RecordOfServiceBasicResponse>>(response_, headers_, cancellationToken).ConfigureAwait(false);
						if (objectResponse_.Object == null)
						{
							throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
						}
						return objectResponse_.Object;
					}
					else
					{
						var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
						throw new ApiException("The HTTP status code of the response was not expected (" + status_ + ").", status_, responseData_, headers_, null);
					}
				}
				finally
				{
					if (disposeResponse_)
						response_.Dispose();
				}
			}
		}
		finally
		{
			if (disposeClient_)
				client_.Dispose();
		}
	}

	/// <returns>Success</returns>
	/// <exception cref="ApiException">A server side error occurred.</exception>
	public virtual System.Threading.Tasks.Task<RecordOfServiceDetailedResponse> Get_ros_by_idAsync(int rosId)
	{
		return Get_ros_by_idAsync(rosId, System.Threading.CancellationToken.None);
	}

	/// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
	/// <returns>Success</returns>
	/// <exception cref="ApiException">A server side error occurred.</exception>
	public virtual async System.Threading.Tasks.Task<RecordOfServiceDetailedResponse> Get_ros_by_idAsync(int rosId, System.Threading.CancellationToken cancellationToken)
	{
		if (rosId == null)
			throw new System.ArgumentNullException("rosId");

		var urlBuilder_ = new System.Text.StringBuilder();
		urlBuilder_.Append("api/RecordOfService/get_ros_by_id/{rosId}");
		urlBuilder_.Replace("{rosId}", System.Uri.EscapeDataString(ConvertToString(rosId, System.Globalization.CultureInfo.InvariantCulture)));

		var client_ = _httpClient;
		var disposeClient_ = false;
		try
		{
			using (var request_ = new System.Net.Http.HttpRequestMessage())
			{
				request_.Method = new System.Net.Http.HttpMethod("GET");
				request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("text/plain"));

				PrepareRequest(client_, request_, urlBuilder_);

				var url_ = urlBuilder_.ToString();
				request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);

				PrepareRequest(client_, request_, url_);

				var response_ = await client_.SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
				var disposeResponse_ = true;
				try
				{
					var headers_ = System.Linq.Enumerable.ToDictionary(response_.Headers, h_ => h_.Key, h_ => h_.Value);
					if (response_.Content != null && response_.Content.Headers != null)
					{
						foreach (var item_ in response_.Content.Headers)
							headers_[item_.Key] = item_.Value;
					}

					ProcessResponse(client_, response_);

					var status_ = (int)response_.StatusCode;
					if (status_ == 200)
					{
						var objectResponse_ = await ReadObjectResponseAsync<RecordOfServiceDetailedResponse>(response_, headers_, cancellationToken).ConfigureAwait(false);
						if (objectResponse_.Object == null)
						{
							throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
						}
						return objectResponse_.Object;
					}
					else
					{
						var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
						throw new ApiException("The HTTP status code of the response was not expected (" + status_ + ").", status_, responseData_, headers_, null);
					}
				}
				finally
				{
					if (disposeResponse_)
						response_.Dispose();
				}
			}
		}
		finally
		{
			if (disposeClient_)
				client_.Dispose();
		}
	}

//	/// <returns>Success</returns>
//	/// <exception cref="ApiException">A server side error occurred.</exception>
//	public virtual System.Threading.Tasks.Task<RecordOfServiceDetailedResponse> Create_rosAsync(int clientId, CreateRecordOfServiceRequest body)
//	{
//		return Create_rosAsync(clientId, body, System.Threading.CancellationToken.None);
//	}
//
//	/// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
//	/// <returns>Success</returns>
//	/// <exception cref="ApiException">A server side error occurred.</exception>
//	public virtual async System.Threading.Tasks.Task<RecordOfServiceDetailedResponse> Create_rosAsync(int clientId, CreateRecordOfServiceRequest body, System.Threading.CancellationToken cancellationToken)
//	{
//		if (clientId == null)
//			throw new System.ArgumentNullException("clientId");
//
//		var urlBuilder_ = new System.Text.StringBuilder();
//		urlBuilder_.Append("api/RecordOfService/create_ros/{clientId}");
//		urlBuilder_.Replace("{clientId}", System.Uri.EscapeDataString(ConvertToString(clientId, System.Globalization.CultureInfo.InvariantCulture)));
//
//		var client_ = _httpClient;
//		var disposeClient_ = false;
//		try
//		{
//			using (var request_ = new System.Net.Http.HttpRequestMessage())
//			{
//				var json_ = Newtonsoft.Json.JsonConvert.SerializeObject(body, _settings.Value);
//				var content_ = new System.Net.Http.StringContent(json_);
//				content_.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json");
//				request_.Content = content_;
//				request_.Method = new System.Net.Http.HttpMethod("POST");
//				request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("text/plain"));
//
//				PrepareRequest(client_, request_, urlBuilder_);
//
//				var url_ = urlBuilder_.ToString();
//				request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);
//
//				PrepareRequest(client_, request_, url_);
//
//				var response_ = await client_.SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
//				var disposeResponse_ = true;
//				try
//				{
//					var headers_ = System.Linq.Enumerable.ToDictionary(response_.Headers, h_ => h_.Key, h_ => h_.Value);
//					if (response_.Content != null && response_.Content.Headers != null)
//					{
//						foreach (var item_ in response_.Content.Headers)
//							headers_[item_.Key] = item_.Value;
//					}
//
//					ProcessResponse(client_, response_);
//
//					var status_ = (int)response_.StatusCode;
//					if (status_ == 200)
//					{
//						var objectResponse_ = await ReadObjectResponseAsync<RecordOfServiceDetailedResponse>(response_, headers_, cancellationToken).ConfigureAwait(false);
//						if (objectResponse_.Object == null)
//						{
//							throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
//						}
//						return objectResponse_.Object;
//					}
//					else
//					{
//						var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
//						throw new ApiException("The HTTP status code of the response was not expected (" + status_ + ").", status_, responseData_, headers_, null);
//					}
//				}
//				finally
//				{
//					if (disposeResponse_)
//						response_.Dispose();
//				}
//			}
//		}
//		finally
//		{
//			if (disposeClient_)
//				client_.Dispose();
//		}
//	}
//
//	/// <returns>Success</returns>
//	/// <exception cref="ApiException">A server side error occurred.</exception>
//	public virtual System.Threading.Tasks.Task<RecordOfServiceDetailedResponse> Add_visitAsync(int rosId, AddVisitsToRecordOfServiceRequest body)
//	{
//		return Add_visitAsync(rosId, body, System.Threading.CancellationToken.None);
//	}
//
//	/// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
//	/// <returns>Success</returns>
//	/// <exception cref="ApiException">A server side error occurred.</exception>
//	public virtual async System.Threading.Tasks.Task<RecordOfServiceDetailedResponse> Add_visitAsync(int rosId, AddVisitsToRecordOfServiceRequest body, System.Threading.CancellationToken cancellationToken)
//	{
//		if (rosId == null)
//			throw new System.ArgumentNullException("rosId");
//
//		var urlBuilder_ = new System.Text.StringBuilder();
//		urlBuilder_.Append("api/RecordOfService/add_visit/{rosId}");
//		urlBuilder_.Replace("{rosId}", System.Uri.EscapeDataString(ConvertToString(rosId, System.Globalization.CultureInfo.InvariantCulture)));
//
//		var client_ = _httpClient;
//		var disposeClient_ = false;
//		try
//		{
//			using (var request_ = new System.Net.Http.HttpRequestMessage())
//			{
//				var json_ = Newtonsoft.Json.JsonConvert.SerializeObject(body, _settings.Value);
//				var content_ = new System.Net.Http.StringContent(json_);
//				content_.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json");
//				request_.Content = content_;
//				request_.Method = new System.Net.Http.HttpMethod("POST");
//				request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("text/plain"));
//
//				PrepareRequest(client_, request_, urlBuilder_);
//
//				var url_ = urlBuilder_.ToString();
//				request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);
//
//				PrepareRequest(client_, request_, url_);
//
//				var response_ = await client_.SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
//				var disposeResponse_ = true;
//				try
//				{
//					var headers_ = System.Linq.Enumerable.ToDictionary(response_.Headers, h_ => h_.Key, h_ => h_.Value);
//					if (response_.Content != null && response_.Content.Headers != null)
//					{
//						foreach (var item_ in response_.Content.Headers)
//							headers_[item_.Key] = item_.Value;
//					}
//
//					ProcessResponse(client_, response_);
//
//					var status_ = (int)response_.StatusCode;
//					if (status_ == 200)
//					{
//						var objectResponse_ = await ReadObjectResponseAsync<RecordOfServiceDetailedResponse>(response_, headers_, cancellationToken).ConfigureAwait(false);
//						if (objectResponse_.Object == null)
//						{
//							throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
//						}
//						return objectResponse_.Object;
//					}
//					else
//					{
//						var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
//						throw new ApiException("The HTTP status code of the response was not expected (" + status_ + ").", status_, responseData_, headers_, null);
//					}
//				}
//				finally
//				{
//					if (disposeResponse_)
//						response_.Dispose();
//				}
//			}
//		}
//		finally
//		{
//			if (disposeClient_)
//				client_.Dispose();
//		}
//	}

	/// <returns>Success</returns>
	/// <exception cref="ApiException">A server side error occurred.</exception>
	public virtual System.Threading.Tasks.Task<RecordOfServiceDetailedResponse> Recalc_rosAsync(int rosId)
	{
		return Recalc_rosAsync(rosId, System.Threading.CancellationToken.None);
	}

	/// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
	/// <returns>Success</returns>
	/// <exception cref="ApiException">A server side error occurred.</exception>
	public virtual async System.Threading.Tasks.Task<RecordOfServiceDetailedResponse> Recalc_rosAsync(int rosId, System.Threading.CancellationToken cancellationToken)
	{
		if (rosId == null)
			throw new System.ArgumentNullException("rosId");

		var urlBuilder_ = new System.Text.StringBuilder();
		urlBuilder_.Append("api/RecordOfService/recalc_ros/{rosId}");
		urlBuilder_.Replace("{rosId}", System.Uri.EscapeDataString(ConvertToString(rosId, System.Globalization.CultureInfo.InvariantCulture)));

		var client_ = _httpClient;
		var disposeClient_ = false;
		try
		{
			using (var request_ = new System.Net.Http.HttpRequestMessage())
			{
				request_.Method = new System.Net.Http.HttpMethod("GET");
				request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("text/plain"));

				PrepareRequest(client_, request_, urlBuilder_);

				var url_ = urlBuilder_.ToString();
				request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);

				PrepareRequest(client_, request_, url_);

				var response_ = await client_.SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
				var disposeResponse_ = true;
				try
				{
					var headers_ = System.Linq.Enumerable.ToDictionary(response_.Headers, h_ => h_.Key, h_ => h_.Value);
					if (response_.Content != null && response_.Content.Headers != null)
					{
						foreach (var item_ in response_.Content.Headers)
							headers_[item_.Key] = item_.Value;
					}

					ProcessResponse(client_, response_);

					var status_ = (int)response_.StatusCode;
					if (status_ == 200)
					{
						var objectResponse_ = await ReadObjectResponseAsync<RecordOfServiceDetailedResponse>(response_, headers_, cancellationToken).ConfigureAwait(false);
						if (objectResponse_.Object == null)
						{
							throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
						}
						return objectResponse_.Object;
					}
					else
					{
						var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
						throw new ApiException("The HTTP status code of the response was not expected (" + status_ + ").", status_, responseData_, headers_, null);
					}
				}
				finally
				{
					if (disposeResponse_)
						response_.Dispose();
				}
			}
		}
		finally
		{
			if (disposeClient_)
				client_.Dispose();
		}
	}

	protected struct ObjectResponseResult<T>
	{
		public ObjectResponseResult(T responseObject, string responseText)
		{
			this.Object = responseObject;
			this.Text = responseText;
		}

		public T Object { get; }

		public string Text { get; }
	}

	public bool ReadResponseAsString { get; set; }

	protected virtual async System.Threading.Tasks.Task<ObjectResponseResult<T>> ReadObjectResponseAsync<T>(System.Net.Http.HttpResponseMessage response, System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IEnumerable<string>> headers, System.Threading.CancellationToken cancellationToken)
	{
		if (response == null || response.Content == null)
		{
			return new ObjectResponseResult<T>(default(T), string.Empty);
		}

		if (ReadResponseAsString)
		{
			var responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
			try
			{
				var typedBody = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(responseText, JsonSerializerSettings);
				return new ObjectResponseResult<T>(typedBody, responseText);
			}
			catch (Newtonsoft.Json.JsonException exception)
			{
				var message = "Could not deserialize the response body string as " + typeof(T).FullName + ".";
				throw new ApiException(message, (int)response.StatusCode, responseText, headers, exception);
			}
		}
		else
		{
			try
			{
				using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
				using (var streamReader = new System.IO.StreamReader(responseStream))
				using (var jsonTextReader = new Newtonsoft.Json.JsonTextReader(streamReader))
				{
					var serializer = Newtonsoft.Json.JsonSerializer.Create(JsonSerializerSettings);
					var typedBody = serializer.Deserialize<T>(jsonTextReader);
					return new ObjectResponseResult<T>(typedBody, string.Empty);
				}
			}
			catch (Newtonsoft.Json.JsonException exception)
			{
				var message = "Could not deserialize the response body stream as " + typeof(T).FullName + ".";
				throw new ApiException(message, (int)response.StatusCode, string.Empty, headers, exception);
			}
		}
	}

	private string ConvertToString(object value, System.Globalization.CultureInfo cultureInfo)
	{
		if (value == null)
		{
			return "";
		}

		if (value is System.Enum)
		{
			var name = System.Enum.GetName(value.GetType(), value);
			if (name != null)
			{
				var field = System.Reflection.IntrospectionExtensions.GetTypeInfo(value.GetType()).GetDeclaredField(name);
				if (field != null)
				{
					var attribute = System.Reflection.CustomAttributeExtensions.GetCustomAttribute(field, typeof(System.Runtime.Serialization.EnumMemberAttribute))
						as System.Runtime.Serialization.EnumMemberAttribute;
					if (attribute != null)
					{
						return attribute.Value != null ? attribute.Value : name;
					}
				}

				var converted = System.Convert.ToString(System.Convert.ChangeType(value, System.Enum.GetUnderlyingType(value.GetType()), cultureInfo));
				return converted == null ? string.Empty : converted;
			}
		}
		else if (value is bool)
		{
			return System.Convert.ToString((bool)value, cultureInfo).ToLowerInvariant();
		}
		else if (value is byte[])
		{
			return System.Convert.ToBase64String((byte[])value);
		}
		else if (value.GetType().IsArray)
		{
			var array = System.Linq.Enumerable.OfType<object>((System.Array)value);
			return string.Join(",", System.Linq.Enumerable.Select(array, o => ConvertToString(o, cultureInfo)));
		}

		var result = System.Convert.ToString(value, cultureInfo);
		return result == null ? "" : result;
	}
}











public partial class GetServiceTypeListItemResponse
{
	[Newtonsoft.Json.JsonProperty("id", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public int Id { get; set; }

	[Newtonsoft.Json.JsonProperty("displayName", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public string DisplayName { get; set; }

	[Newtonsoft.Json.JsonProperty("basePrice", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public double BasePrice { get; set; }

	[Newtonsoft.Json.JsonProperty("ignoreQtyForTotal", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public bool IgnoreQtyForTotal { get; set; }

	[Newtonsoft.Json.JsonProperty("isRetired", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public bool IsRetired { get; set; }

	[Newtonsoft.Json.JsonProperty("dateRetired", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public System.DateTimeOffset? DateRetired { get; set; }

	[Newtonsoft.Json.JsonProperty("retiredBy", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public string RetiredBy { get; set; }

	[Newtonsoft.Json.JsonProperty("visitCount", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public int VisitCount { get; set; }

	[Newtonsoft.Json.JsonProperty("options", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public System.Collections.Generic.ICollection<ServiceTypeOptionListItemResponse> Options { get; set; }

}

public partial class ServiceTypeOptionListItemResponse
{
	[Newtonsoft.Json.JsonProperty("id", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public int Id { get; set; }

	[Newtonsoft.Json.JsonProperty("displayName", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public string DisplayName { get; set; }

	[Newtonsoft.Json.JsonProperty("basePrice", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public double BasePrice { get; set; }

	[Newtonsoft.Json.JsonProperty("ignoreQtyForTotal", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public bool IgnoreQtyForTotal { get; set; }

	[Newtonsoft.Json.JsonProperty("isRetired", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public bool IsRetired { get; set; }

	[Newtonsoft.Json.JsonProperty("dateRetired", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public System.DateTimeOffset? DateRetired { get; set; }

	[Newtonsoft.Json.JsonProperty("retiredBy", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public string RetiredBy { get; set; }

	[Newtonsoft.Json.JsonProperty("visitCount", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public int VisitCount { get; set; }

}

public partial class ServiceTypeClient 
    {
        private System.Net.Http.HttpClient _httpClient;
        private System.Lazy<Newtonsoft.Json.JsonSerializerSettings> _settings;

        public ServiceTypeClient(System.Net.Http.HttpClient httpClient)
        {
            _httpClient = httpClient;
            _settings = new System.Lazy<Newtonsoft.Json.JsonSerializerSettings>(CreateSerializerSettings, true);
        }

        private Newtonsoft.Json.JsonSerializerSettings CreateSerializerSettings()
        {
            var settings = new Newtonsoft.Json.JsonSerializerSettings();
            UpdateJsonSerializerSettings(settings);
            return settings;
        }

        protected Newtonsoft.Json.JsonSerializerSettings JsonSerializerSettings { get { return _settings.Value; } }

        partial void UpdateJsonSerializerSettings(Newtonsoft.Json.JsonSerializerSettings settings);

        partial void PrepareRequest(System.Net.Http.HttpClient client, System.Net.Http.HttpRequestMessage request, string url);
        partial void PrepareRequest(System.Net.Http.HttpClient client, System.Net.Http.HttpRequestMessage request, System.Text.StringBuilder urlBuilder);
        partial void ProcessResponse(System.Net.Http.HttpClient client, System.Net.Http.HttpResponseMessage response);

        /// <returns>Success</returns>
        /// <exception cref="ApiException">A server side error occurred.</exception>
        public virtual System.Threading.Tasks.Task<System.Collections.Generic.ICollection<GetServiceTypeListItemResponse>> Get_allAsync()
        {
            return Get_allAsync(System.Threading.CancellationToken.None);
        }

        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Success</returns>
        /// <exception cref="ApiException">A server side error occurred.</exception>
        public virtual async System.Threading.Tasks.Task<System.Collections.Generic.ICollection<GetServiceTypeListItemResponse>> Get_allAsync(System.Threading.CancellationToken cancellationToken)
        {
            var urlBuilder_ = new System.Text.StringBuilder();
            urlBuilder_.Append("api/ServiceType/get_all");

            var client_ = _httpClient;
            var disposeClient_ = false;
            try
            {
                using (var request_ = new System.Net.Http.HttpRequestMessage())
                {
                    request_.Method = new System.Net.Http.HttpMethod("GET");
                    request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("text/plain"));

                    PrepareRequest(client_, request_, urlBuilder_);

                    var url_ = urlBuilder_.ToString();
                    request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);

                    PrepareRequest(client_, request_, url_);

                    var response_ = await client_.SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                    var disposeResponse_ = true;
                    try
                    {
                        var headers_ = System.Linq.Enumerable.ToDictionary(response_.Headers, h_ => h_.Key, h_ => h_.Value);
                        if (response_.Content != null && response_.Content.Headers != null)
                        {
                            foreach (var item_ in response_.Content.Headers)
                                headers_[item_.Key] = item_.Value;
                        }

                        ProcessResponse(client_, response_);

                        var status_ = (int)response_.StatusCode;
                        if (status_ == 200)
                        {
                            var objectResponse_ = await ReadObjectResponseAsync<System.Collections.Generic.ICollection<GetServiceTypeListItemResponse>>(response_, headers_, cancellationToken).ConfigureAwait(false);
                            if (objectResponse_.Object == null)
                            {
                                throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
                            }
                            return objectResponse_.Object;
                        }
                        else
                        {
                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            throw new ApiException("The HTTP status code of the response was not expected (" + status_ + ").", status_, responseData_, headers_, null);
                        }
                    }
                    finally
                    {
                        if (disposeResponse_)
                            response_.Dispose();
                    }
                }
            }
            finally
            {
                if (disposeClient_)
                    client_.Dispose();
            }
        }

        /// <returns>Success</returns>
        /// <exception cref="ApiException">A server side error occurred.</exception>
        public virtual System.Threading.Tasks.Task<GetServiceTypeListItemResponse> Get_servicetype_by_idAsync(int id)
        {
            return Get_servicetype_by_idAsync(id, System.Threading.CancellationToken.None);
        }

        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Success</returns>
        /// <exception cref="ApiException">A server side error occurred.</exception>
        public virtual async System.Threading.Tasks.Task<GetServiceTypeListItemResponse> Get_servicetype_by_idAsync(int id, System.Threading.CancellationToken cancellationToken)
        {
            if (id == null)
                throw new System.ArgumentNullException("id");

            var urlBuilder_ = new System.Text.StringBuilder();
            urlBuilder_.Append("api/ServiceType/get_servicetype_by_id/{id}");
            urlBuilder_.Replace("{id}", System.Uri.EscapeDataString(ConvertToString(id, System.Globalization.CultureInfo.InvariantCulture)));

            var client_ = _httpClient;
            var disposeClient_ = false;
            try
            {
                using (var request_ = new System.Net.Http.HttpRequestMessage())
                {
                    request_.Method = new System.Net.Http.HttpMethod("GET");
                    request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("text/plain"));

                    PrepareRequest(client_, request_, urlBuilder_);

                    var url_ = urlBuilder_.ToString();
                    request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);

                    PrepareRequest(client_, request_, url_);

                    var response_ = await client_.SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                    var disposeResponse_ = true;
                    try
                    {
                        var headers_ = System.Linq.Enumerable.ToDictionary(response_.Headers, h_ => h_.Key, h_ => h_.Value);
                        if (response_.Content != null && response_.Content.Headers != null)
                        {
                            foreach (var item_ in response_.Content.Headers)
                                headers_[item_.Key] = item_.Value;
                        }

                        ProcessResponse(client_, response_);

                        var status_ = (int)response_.StatusCode;
                        if (status_ == 200)
                        {
                            var objectResponse_ = await ReadObjectResponseAsync<GetServiceTypeListItemResponse>(response_, headers_, cancellationToken).ConfigureAwait(false);
                            if (objectResponse_.Object == null)
                            {
                                throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
                            }
                            return objectResponse_.Object;
                        }
                        else
                        if (status_ == 404)
                        {
                            var objectResponse_ = await ReadObjectResponseAsync<ProblemDetails>(response_, headers_, cancellationToken).ConfigureAwait(false);
                            if (objectResponse_.Object == null)
                            {
                                throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
                            }
                            throw new ApiException<ProblemDetails>("Not Found", status_, objectResponse_.Text, headers_, objectResponse_.Object, null);
                        }
                        else
                        {
                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            throw new ApiException("The HTTP status code of the response was not expected (" + status_ + ").", status_, responseData_, headers_, null);
                        }
                    }
                    finally
                    {
                        if (disposeResponse_)
                            response_.Dispose();
                    }
                }
            }
            finally
            {
                if (disposeClient_)
                    client_.Dispose();
            }
        }

//        /// <returns>Success</returns>
//        /// <exception cref="ApiException">A server side error occurred.</exception>
//        public virtual System.Threading.Tasks.Task<CreateServiceTypeResponse> Create_service_typeAsync(CreateServiceTypeRequest body)
//        {
//            return Create_service_typeAsync(body, System.Threading.CancellationToken.None);
//        }
//
//        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
//        /// <returns>Success</returns>
//        /// <exception cref="ApiException">A server side error occurred.</exception>
//        public virtual async System.Threading.Tasks.Task<CreateServiceTypeResponse> Create_service_typeAsync(CreateServiceTypeRequest body, System.Threading.CancellationToken cancellationToken)
//        {
//            var urlBuilder_ = new System.Text.StringBuilder();
//            urlBuilder_.Append("api/ServiceType/create_service_type");
//
//            var client_ = _httpClient;
//            var disposeClient_ = false;
//            try
//            {
//                using (var request_ = new System.Net.Http.HttpRequestMessage())
//                {
//                    var json_ = Newtonsoft.Json.JsonConvert.SerializeObject(body, _settings.Value);
//                    var content_ = new System.Net.Http.StringContent(json_);
//                    content_.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json");
//                    request_.Content = content_;
//                    request_.Method = new System.Net.Http.HttpMethod("POST");
//                    request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("text/plain"));
//
//                    PrepareRequest(client_, request_, urlBuilder_);
//
//                    var url_ = urlBuilder_.ToString();
//                    request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);
//
//                    PrepareRequest(client_, request_, url_);
//
//                    var response_ = await client_.SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
//                    var disposeResponse_ = true;
//                    try
//                    {
//                        var headers_ = System.Linq.Enumerable.ToDictionary(response_.Headers, h_ => h_.Key, h_ => h_.Value);
//                        if (response_.Content != null && response_.Content.Headers != null)
//                        {
//                            foreach (var item_ in response_.Content.Headers)
//                                headers_[item_.Key] = item_.Value;
//                        }
//
//                        ProcessResponse(client_, response_);
//
//                        var status_ = (int)response_.StatusCode;
//                        if (status_ == 200)
//                        {
//                            var objectResponse_ = await ReadObjectResponseAsync<CreateServiceTypeResponse>(response_, headers_, cancellationToken).ConfigureAwait(false);
//                            if (objectResponse_.Object == null)
//                            {
//                                throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
//                            }
//                            return objectResponse_.Object;
//                        }
//                        else
//                        {
//                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
//                            throw new ApiException("The HTTP status code of the response was not expected (" + status_ + ").", status_, responseData_, headers_, null);
//                        }
//                    }
//                    finally
//                    {
//                        if (disposeResponse_)
//                            response_.Dispose();
//                    }
//                }
//            }
//            finally
//            {
//                if (disposeClient_)
//                    client_.Dispose();
//            }
//        }
//
//        /// <returns>Success</returns>
//        /// <exception cref="ApiException">A server side error occurred.</exception>
//        public virtual System.Threading.Tasks.Task<EditServiceTypeResponse> Edit_service_typeAsync(int serviceTypeId, EditServiceTypeRequest body)
//        {
//            return Edit_service_typeAsync(serviceTypeId, body, System.Threading.CancellationToken.None);
//        }
//
//        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
//        /// <returns>Success</returns>
//        /// <exception cref="ApiException">A server side error occurred.</exception>
//        public virtual async System.Threading.Tasks.Task<EditServiceTypeResponse> Edit_service_typeAsync(int serviceTypeId, EditServiceTypeRequest body, System.Threading.CancellationToken cancellationToken)
//        {
//            if (serviceTypeId == null)
//                throw new System.ArgumentNullException("serviceTypeId");
//
//            var urlBuilder_ = new System.Text.StringBuilder();
//            urlBuilder_.Append("api/ServiceType/edit_service_type/{serviceTypeId}");
//            urlBuilder_.Replace("{serviceTypeId}", System.Uri.EscapeDataString(ConvertToString(serviceTypeId, System.Globalization.CultureInfo.InvariantCulture)));
//
//            var client_ = _httpClient;
//            var disposeClient_ = false;
//            try
//            {
//                using (var request_ = new System.Net.Http.HttpRequestMessage())
//                {
//                    var json_ = Newtonsoft.Json.JsonConvert.SerializeObject(body, _settings.Value);
//                    var content_ = new System.Net.Http.StringContent(json_);
//                    content_.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json");
//                    request_.Content = content_;
//                    request_.Method = new System.Net.Http.HttpMethod("POST");
//                    request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("text/plain"));
//
//                    PrepareRequest(client_, request_, urlBuilder_);
//
//                    var url_ = urlBuilder_.ToString();
//                    request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);
//
//                    PrepareRequest(client_, request_, url_);
//
//                    var response_ = await client_.SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
//                    var disposeResponse_ = true;
//                    try
//                    {
//                        var headers_ = System.Linq.Enumerable.ToDictionary(response_.Headers, h_ => h_.Key, h_ => h_.Value);
//                        if (response_.Content != null && response_.Content.Headers != null)
//                        {
//                            foreach (var item_ in response_.Content.Headers)
//                                headers_[item_.Key] = item_.Value;
//                        }
//
//                        ProcessResponse(client_, response_);
//
//                        var status_ = (int)response_.StatusCode;
//                        if (status_ == 200)
//                        {
//                            var objectResponse_ = await ReadObjectResponseAsync<EditServiceTypeResponse>(response_, headers_, cancellationToken).ConfigureAwait(false);
//                            if (objectResponse_.Object == null)
//                            {
//                                throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
//                            }
//                            return objectResponse_.Object;
//                        }
//                        else
//                        if (status_ == 404)
//                        {
//                            var objectResponse_ = await ReadObjectResponseAsync<ProblemDetails>(response_, headers_, cancellationToken).ConfigureAwait(false);
//                            if (objectResponse_.Object == null)
//                            {
//                                throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
//                            }
//                            throw new ApiException<ProblemDetails>("Not Found", status_, objectResponse_.Text, headers_, objectResponse_.Object, null);
//                        }
//                        else
//                        {
//                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
//                            throw new ApiException("The HTTP status code of the response was not expected (" + status_ + ").", status_, responseData_, headers_, null);
//                        }
//                    }
//                    finally
//                    {
//                        if (disposeResponse_)
//                            response_.Dispose();
//                    }
//                }
//            }
//            finally
//            {
//                if (disposeClient_)
//                    client_.Dispose();
//            }
//        }
//
//        /// <returns>Success</returns>
//        /// <exception cref="ApiException">A server side error occurred.</exception>
//        public virtual System.Threading.Tasks.Task<ServiceTypeOptionEntity> Create_optionAsync(int serviceTypeId, CreateServiceTypeOptionRequest body)
//        {
//            return Create_optionAsync(serviceTypeId, body, System.Threading.CancellationToken.None);
//        }
//
//        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
//        /// <returns>Success</returns>
//        /// <exception cref="ApiException">A server side error occurred.</exception>
//        public virtual async System.Threading.Tasks.Task<ServiceTypeOptionEntity> Create_optionAsync(int serviceTypeId, CreateServiceTypeOptionRequest body, System.Threading.CancellationToken cancellationToken)
//        {
//            if (serviceTypeId == null)
//                throw new System.ArgumentNullException("serviceTypeId");
//
//            var urlBuilder_ = new System.Text.StringBuilder();
//            urlBuilder_.Append("api/ServiceType/create_option/{serviceTypeId}");
//            urlBuilder_.Replace("{serviceTypeId}", System.Uri.EscapeDataString(ConvertToString(serviceTypeId, System.Globalization.CultureInfo.InvariantCulture)));
//
//            var client_ = _httpClient;
//            var disposeClient_ = false;
//            try
//            {
//                using (var request_ = new System.Net.Http.HttpRequestMessage())
//                {
//                    var json_ = Newtonsoft.Json.JsonConvert.SerializeObject(body, _settings.Value);
//                    var content_ = new System.Net.Http.StringContent(json_);
//                    content_.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json");
//                    request_.Content = content_;
//                    request_.Method = new System.Net.Http.HttpMethod("POST");
//                    request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("text/plain"));
//
//                    PrepareRequest(client_, request_, urlBuilder_);
//
//                    var url_ = urlBuilder_.ToString();
//                    request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);
//
//                    PrepareRequest(client_, request_, url_);
//
//                    var response_ = await client_.SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
//                    var disposeResponse_ = true;
//                    try
//                    {
//                        var headers_ = System.Linq.Enumerable.ToDictionary(response_.Headers, h_ => h_.Key, h_ => h_.Value);
//                        if (response_.Content != null && response_.Content.Headers != null)
//                        {
//                            foreach (var item_ in response_.Content.Headers)
//                                headers_[item_.Key] = item_.Value;
//                        }
//
//                        ProcessResponse(client_, response_);
//
//                        var status_ = (int)response_.StatusCode;
//                        if (status_ == 200)
//                        {
//                            var objectResponse_ = await ReadObjectResponseAsync<ServiceTypeOptionEntity>(response_, headers_, cancellationToken).ConfigureAwait(false);
//                            if (objectResponse_.Object == null)
//                            {
//                                throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
//                            }
//                            return objectResponse_.Object;
//                        }
//                        else
//                        if (status_ == 404)
//                        {
//                            var objectResponse_ = await ReadObjectResponseAsync<ProblemDetails>(response_, headers_, cancellationToken).ConfigureAwait(false);
//                            if (objectResponse_.Object == null)
//                            {
//                                throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
//                            }
//                            throw new ApiException<ProblemDetails>("Not Found", status_, objectResponse_.Text, headers_, objectResponse_.Object, null);
//                        }
//                        else
//                        {
//                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
//                            throw new ApiException("The HTTP status code of the response was not expected (" + status_ + ").", status_, responseData_, headers_, null);
//                        }
//                    }
//                    finally
//                    {
//                        if (disposeResponse_)
//                            response_.Dispose();
//                    }
//                }
//            }
//            finally
//            {
//                if (disposeClient_)
//                    client_.Dispose();
//            }
//        }
//
//        /// <returns>Success</returns>
//        /// <exception cref="ApiException">A server side error occurred.</exception>
//        public virtual System.Threading.Tasks.Task<EditServiceTypeOptionResponse> Edit_optionAsync(int optionId, EditServiceTypeOptionRequest body)
//        {
//            return Edit_optionAsync(optionId, body, System.Threading.CancellationToken.None);
//        }
//
//        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
//        /// <returns>Success</returns>
//        /// <exception cref="ApiException">A server side error occurred.</exception>
//        public virtual async System.Threading.Tasks.Task<EditServiceTypeOptionResponse> Edit_optionAsync(int optionId, EditServiceTypeOptionRequest body, System.Threading.CancellationToken cancellationToken)
//        {
//            if (optionId == null)
//                throw new System.ArgumentNullException("optionId");
//
//            var urlBuilder_ = new System.Text.StringBuilder();
//            urlBuilder_.Append("api/ServiceType/edit_option/{optionId}");
//            urlBuilder_.Replace("{optionId}", System.Uri.EscapeDataString(ConvertToString(optionId, System.Globalization.CultureInfo.InvariantCulture)));
//
//            var client_ = _httpClient;
//            var disposeClient_ = false;
//            try
//            {
//                using (var request_ = new System.Net.Http.HttpRequestMessage())
//                {
//                    var json_ = Newtonsoft.Json.JsonConvert.SerializeObject(body, _settings.Value);
//                    var content_ = new System.Net.Http.StringContent(json_);
//                    content_.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json");
//                    request_.Content = content_;
//                    request_.Method = new System.Net.Http.HttpMethod("POST");
//                    request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("text/plain"));
//
//                    PrepareRequest(client_, request_, urlBuilder_);
//
//                    var url_ = urlBuilder_.ToString();
//                    request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);
//
//                    PrepareRequest(client_, request_, url_);
//
//                    var response_ = await client_.SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
//                    var disposeResponse_ = true;
//                    try
//                    {
//                        var headers_ = System.Linq.Enumerable.ToDictionary(response_.Headers, h_ => h_.Key, h_ => h_.Value);
//                        if (response_.Content != null && response_.Content.Headers != null)
//                        {
//                            foreach (var item_ in response_.Content.Headers)
//                                headers_[item_.Key] = item_.Value;
//                        }
//
//                        ProcessResponse(client_, response_);
//
//                        var status_ = (int)response_.StatusCode;
//                        if (status_ == 200)
//                        {
//                            var objectResponse_ = await ReadObjectResponseAsync<EditServiceTypeOptionResponse>(response_, headers_, cancellationToken).ConfigureAwait(false);
//                            if (objectResponse_.Object == null)
//                            {
//                                throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
//                            }
//                            return objectResponse_.Object;
//                        }
//                        else
//                        if (status_ == 404)
//                        {
//                            var objectResponse_ = await ReadObjectResponseAsync<ProblemDetails>(response_, headers_, cancellationToken).ConfigureAwait(false);
//                            if (objectResponse_.Object == null)
//                            {
//                                throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
//                            }
//                            throw new ApiException<ProblemDetails>("Not Found", status_, objectResponse_.Text, headers_, objectResponse_.Object, null);
//                        }
//                        else
//                        {
//                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
//                            throw new ApiException("The HTTP status code of the response was not expected (" + status_ + ").", status_, responseData_, headers_, null);
//                        }
//                    }
//                    finally
//                    {
//                        if (disposeResponse_)
//                            response_.Dispose();
//                    }
//                }
//            }
//            finally
//            {
//                if (disposeClient_)
//                    client_.Dispose();
//            }
//        }
//
//        /// <returns>Success</returns>
//        /// <exception cref="ApiException">A server side error occurred.</exception>
//        public virtual System.Threading.Tasks.Task<RetireResponse> RetireAsync(int serviceTypeId, RetireRequest body)
//        {
//            return RetireAsync(serviceTypeId, body, System.Threading.CancellationToken.None);
//        }
//
//        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
//        /// <returns>Success</returns>
//        /// <exception cref="ApiException">A server side error occurred.</exception>
//        public virtual async System.Threading.Tasks.Task<RetireResponse> RetireAsync(int serviceTypeId, RetireRequest body, System.Threading.CancellationToken cancellationToken)
//        {
//            if (serviceTypeId == null)
//                throw new System.ArgumentNullException("serviceTypeId");
//
//            if (body == null)
//                throw new System.ArgumentNullException("body");
//
//            var urlBuilder_ = new System.Text.StringBuilder();
//            urlBuilder_.Append("api/ServiceType/retire/{serviceTypeId}");
//            urlBuilder_.Replace("{serviceTypeId}", System.Uri.EscapeDataString(ConvertToString(serviceTypeId, System.Globalization.CultureInfo.InvariantCulture)));
//
//            var client_ = _httpClient;
//            var disposeClient_ = false;
//            try
//            {
//                using (var request_ = new System.Net.Http.HttpRequestMessage())
//                {
//                    var json_ = Newtonsoft.Json.JsonConvert.SerializeObject(body, _settings.Value);
//                    var content_ = new System.Net.Http.StringContent(json_);
//                    content_.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json");
//                    request_.Content = content_;
//                    request_.Method = new System.Net.Http.HttpMethod("POST");
//                    request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("text/plain"));
//
//                    PrepareRequest(client_, request_, urlBuilder_);
//
//                    var url_ = urlBuilder_.ToString();
//                    request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);
//
//                    PrepareRequest(client_, request_, url_);
//
//                    var response_ = await client_.SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
//                    var disposeResponse_ = true;
//                    try
//                    {
//                        var headers_ = System.Linq.Enumerable.ToDictionary(response_.Headers, h_ => h_.Key, h_ => h_.Value);
//                        if (response_.Content != null && response_.Content.Headers != null)
//                        {
//                            foreach (var item_ in response_.Content.Headers)
//                                headers_[item_.Key] = item_.Value;
//                        }
//
//                        ProcessResponse(client_, response_);
//
//                        var status_ = (int)response_.StatusCode;
//                        if (status_ == 200)
//                        {
//                            var objectResponse_ = await ReadObjectResponseAsync<RetireResponse>(response_, headers_, cancellationToken).ConfigureAwait(false);
//                            if (objectResponse_.Object == null)
//                            {
//                                throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
//                            }
//                            return objectResponse_.Object;
//                        }
//                        else
//                        if (status_ == 404)
//                        {
//                            var objectResponse_ = await ReadObjectResponseAsync<ProblemDetails>(response_, headers_, cancellationToken).ConfigureAwait(false);
//                            if (objectResponse_.Object == null)
//                            {
//                                throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
//                            }
//                            throw new ApiException<ProblemDetails>("Not Found", status_, objectResponse_.Text, headers_, objectResponse_.Object, null);
//                        }
//                        else
//                        {
//                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
//                            throw new ApiException("The HTTP status code of the response was not expected (" + status_ + ").", status_, responseData_, headers_, null);
//                        }
//                    }
//                    finally
//                    {
//                        if (disposeResponse_)
//                            response_.Dispose();
//                    }
//                }
//            }
//            finally
//            {
//                if (disposeClient_)
//                    client_.Dispose();
//            }
//        }
//
//        /// <returns>Success</returns>
//        /// <exception cref="ApiException">A server side error occurred.</exception>
//        public virtual System.Threading.Tasks.Task<UnretireResponse> UnretireAsync(int serviceTypeId)
//        {
//            return UnretireAsync(serviceTypeId, System.Threading.CancellationToken.None);
//        }
//
//        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
//        /// <returns>Success</returns>
//        /// <exception cref="ApiException">A server side error occurred.</exception>
//        public virtual async System.Threading.Tasks.Task<UnretireResponse> UnretireAsync(int serviceTypeId, System.Threading.CancellationToken cancellationToken)
//        {
//            if (serviceTypeId == null)
//                throw new System.ArgumentNullException("serviceTypeId");
//
//            var urlBuilder_ = new System.Text.StringBuilder();
//            urlBuilder_.Append("api/ServiceType/unretire/{serviceTypeId}");
//            urlBuilder_.Replace("{serviceTypeId}", System.Uri.EscapeDataString(ConvertToString(serviceTypeId, System.Globalization.CultureInfo.InvariantCulture)));
//
//            var client_ = _httpClient;
//            var disposeClient_ = false;
//            try
//            {
//                using (var request_ = new System.Net.Http.HttpRequestMessage())
//                {
//                    request_.Content = new System.Net.Http.StringContent(string.Empty, System.Text.Encoding.UTF8, "text/plain");
//                    request_.Method = new System.Net.Http.HttpMethod("POST");
//                    request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("text/plain"));
//
//                    PrepareRequest(client_, request_, urlBuilder_);
//
//                    var url_ = urlBuilder_.ToString();
//                    request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);
//
//                    PrepareRequest(client_, request_, url_);
//
//                    var response_ = await client_.SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
//                    var disposeResponse_ = true;
//                    try
//                    {
//                        var headers_ = System.Linq.Enumerable.ToDictionary(response_.Headers, h_ => h_.Key, h_ => h_.Value);
//                        if (response_.Content != null && response_.Content.Headers != null)
//                        {
//                            foreach (var item_ in response_.Content.Headers)
//                                headers_[item_.Key] = item_.Value;
//                        }
//
//                        ProcessResponse(client_, response_);
//
//                        var status_ = (int)response_.StatusCode;
//                        if (status_ == 200)
//                        {
//                            var objectResponse_ = await ReadObjectResponseAsync<UnretireResponse>(response_, headers_, cancellationToken).ConfigureAwait(false);
//                            if (objectResponse_.Object == null)
//                            {
//                                throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
//                            }
//                            return objectResponse_.Object;
//                        }
//                        else
//                        if (status_ == 404)
//                        {
//                            var objectResponse_ = await ReadObjectResponseAsync<ProblemDetails>(response_, headers_, cancellationToken).ConfigureAwait(false);
//                            if (objectResponse_.Object == null)
//                            {
//                                throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
//                            }
//                            throw new ApiException<ProblemDetails>("Not Found", status_, objectResponse_.Text, headers_, objectResponse_.Object, null);
//                        }
//                        else
//                        {
//                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
//                            throw new ApiException("The HTTP status code of the response was not expected (" + status_ + ").", status_, responseData_, headers_, null);
//                        }
//                    }
//                    finally
//                    {
//                        if (disposeResponse_)
//                            response_.Dispose();
//                    }
//                }
//            }
//            finally
//            {
//                if (disposeClient_)
//                    client_.Dispose();
//            }
//        }
//
//        /// <returns>Success</returns>
//        /// <exception cref="ApiException">A server side error occurred.</exception>
//        public virtual System.Threading.Tasks.Task<RetireResponse> Retire_optionAsync(int serviceTypeOptionId, RetireRequest body)
//        {
//            return Retire_optionAsync(serviceTypeOptionId, body, System.Threading.CancellationToken.None);
//        }
//
//        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
//        /// <returns>Success</returns>
//        /// <exception cref="ApiException">A server side error occurred.</exception>
//        public virtual async System.Threading.Tasks.Task<RetireResponse> Retire_optionAsync(int serviceTypeOptionId, RetireRequest body, System.Threading.CancellationToken cancellationToken)
//        {
//            if (serviceTypeOptionId == null)
//                throw new System.ArgumentNullException("serviceTypeOptionId");
//
//            if (body == null)
//                throw new System.ArgumentNullException("body");
//
//            var urlBuilder_ = new System.Text.StringBuilder();
//            urlBuilder_.Append("api/ServiceType/retire_option/{serviceTypeOptionId}");
//            urlBuilder_.Replace("{serviceTypeOptionId}", System.Uri.EscapeDataString(ConvertToString(serviceTypeOptionId, System.Globalization.CultureInfo.InvariantCulture)));
//
//            var client_ = _httpClient;
//            var disposeClient_ = false;
//            try
//            {
//                using (var request_ = new System.Net.Http.HttpRequestMessage())
//                {
//                    var json_ = Newtonsoft.Json.JsonConvert.SerializeObject(body, _settings.Value);
//                    var content_ = new System.Net.Http.StringContent(json_);
//                    content_.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json");
//                    request_.Content = content_;
//                    request_.Method = new System.Net.Http.HttpMethod("POST");
//                    request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("text/plain"));
//
//                    PrepareRequest(client_, request_, urlBuilder_);
//
//                    var url_ = urlBuilder_.ToString();
//                    request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);
//
//                    PrepareRequest(client_, request_, url_);
//
//                    var response_ = await client_.SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
//                    var disposeResponse_ = true;
//                    try
//                    {
//                        var headers_ = System.Linq.Enumerable.ToDictionary(response_.Headers, h_ => h_.Key, h_ => h_.Value);
//                        if (response_.Content != null && response_.Content.Headers != null)
//                        {
//                            foreach (var item_ in response_.Content.Headers)
//                                headers_[item_.Key] = item_.Value;
//                        }
//
//                        ProcessResponse(client_, response_);
//
//                        var status_ = (int)response_.StatusCode;
//                        if (status_ == 200)
//                        {
//                            var objectResponse_ = await ReadObjectResponseAsync<RetireResponse>(response_, headers_, cancellationToken).ConfigureAwait(false);
//                            if (objectResponse_.Object == null)
//                            {
//                                throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
//                            }
//                            return objectResponse_.Object;
//                        }
//                        else
//                        if (status_ == 404)
//                        {
//                            var objectResponse_ = await ReadObjectResponseAsync<ProblemDetails>(response_, headers_, cancellationToken).ConfigureAwait(false);
//                            if (objectResponse_.Object == null)
//                            {
//                                throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
//                            }
//                            throw new ApiException<ProblemDetails>("Not Found", status_, objectResponse_.Text, headers_, objectResponse_.Object, null);
//                        }
//                        else
//                        {
//                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
//                            throw new ApiException("The HTTP status code of the response was not expected (" + status_ + ").", status_, responseData_, headers_, null);
//                        }
//                    }
//                    finally
//                    {
//                        if (disposeResponse_)
//                            response_.Dispose();
//                    }
//                }
//            }
//            finally
//            {
//                if (disposeClient_)
//                    client_.Dispose();
//            }
//        }
//
//        /// <returns>Success</returns>
//        /// <exception cref="ApiException">A server side error occurred.</exception>
//        public virtual System.Threading.Tasks.Task<UnretireResponse> Unretire_optionAsync(int serviceTypeOptionId)
//        {
//            return Unretire_optionAsync(serviceTypeOptionId, System.Threading.CancellationToken.None);
//        }
//
//        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
//        /// <returns>Success</returns>
//        /// <exception cref="ApiException">A server side error occurred.</exception>
//        public virtual async System.Threading.Tasks.Task<UnretireResponse> Unretire_optionAsync(int serviceTypeOptionId, System.Threading.CancellationToken cancellationToken)
//        {
//            if (serviceTypeOptionId == null)
//                throw new System.ArgumentNullException("serviceTypeOptionId");
//
//            var urlBuilder_ = new System.Text.StringBuilder();
//            urlBuilder_.Append("api/ServiceType/unretire_option/{serviceTypeOptionId}");
//            urlBuilder_.Replace("{serviceTypeOptionId}", System.Uri.EscapeDataString(ConvertToString(serviceTypeOptionId, System.Globalization.CultureInfo.InvariantCulture)));
//
//            var client_ = _httpClient;
//            var disposeClient_ = false;
//            try
//            {
//                using (var request_ = new System.Net.Http.HttpRequestMessage())
//                {
//                    request_.Content = new System.Net.Http.StringContent(string.Empty, System.Text.Encoding.UTF8, "text/plain");
//                    request_.Method = new System.Net.Http.HttpMethod("POST");
//                    request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("text/plain"));
//
//                    PrepareRequest(client_, request_, urlBuilder_);
//
//                    var url_ = urlBuilder_.ToString();
//                    request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);
//
//                    PrepareRequest(client_, request_, url_);
//
//                    var response_ = await client_.SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
//                    var disposeResponse_ = true;
//                    try
//                    {
//                        var headers_ = System.Linq.Enumerable.ToDictionary(response_.Headers, h_ => h_.Key, h_ => h_.Value);
//                        if (response_.Content != null && response_.Content.Headers != null)
//                        {
//                            foreach (var item_ in response_.Content.Headers)
//                                headers_[item_.Key] = item_.Value;
//                        }
//
//                        ProcessResponse(client_, response_);
//
//                        var status_ = (int)response_.StatusCode;
//                        if (status_ == 200)
//                        {
//                            var objectResponse_ = await ReadObjectResponseAsync<UnretireResponse>(response_, headers_, cancellationToken).ConfigureAwait(false);
//                            if (objectResponse_.Object == null)
//                            {
//                                throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
//                            }
//                            return objectResponse_.Object;
//                        }
//                        else
//                        if (status_ == 404)
//                        {
//                            var objectResponse_ = await ReadObjectResponseAsync<ProblemDetails>(response_, headers_, cancellationToken).ConfigureAwait(false);
//                            if (objectResponse_.Object == null)
//                            {
//                                throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
//                            }
//                            throw new ApiException<ProblemDetails>("Not Found", status_, objectResponse_.Text, headers_, objectResponse_.Object, null);
//                        }
//                        else
//                        {
//                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
//                            throw new ApiException("The HTTP status code of the response was not expected (" + status_ + ").", status_, responseData_, headers_, null);
//                        }
//                    }
//                    finally
//                    {
//                        if (disposeResponse_)
//                            response_.Dispose();
//                    }
//                }
//            }
//            finally
//            {
//                if (disposeClient_)
//                    client_.Dispose();
//            }
//        }

        protected struct ObjectResponseResult<T>
        {
            public ObjectResponseResult(T responseObject, string responseText)
            {
                this.Object = responseObject;
                this.Text = responseText;
            }

            public T Object { get; }

            public string Text { get; }
        }

        public bool ReadResponseAsString { get; set; }

        protected virtual async System.Threading.Tasks.Task<ObjectResponseResult<T>> ReadObjectResponseAsync<T>(System.Net.Http.HttpResponseMessage response, System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IEnumerable<string>> headers, System.Threading.CancellationToken cancellationToken)
        {
            if (response == null || response.Content == null)
            {
                return new ObjectResponseResult<T>(default(T), string.Empty);
            }

            if (ReadResponseAsString)
            {
                var responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                try
                {
                    var typedBody = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(responseText, JsonSerializerSettings);
                    return new ObjectResponseResult<T>(typedBody, responseText);
                }
                catch (Newtonsoft.Json.JsonException exception)
                {
                    var message = "Could not deserialize the response body string as " + typeof(T).FullName + ".";
                    throw new ApiException(message, (int)response.StatusCode, responseText, headers, exception);
                }
            }
            else
            {
                try
                {
                    using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var streamReader = new System.IO.StreamReader(responseStream))
                    using (var jsonTextReader = new Newtonsoft.Json.JsonTextReader(streamReader))
                    {
                        var serializer = Newtonsoft.Json.JsonSerializer.Create(JsonSerializerSettings);
                        var typedBody = serializer.Deserialize<T>(jsonTextReader);
                        return new ObjectResponseResult<T>(typedBody, string.Empty);
                    }
                }
                catch (Newtonsoft.Json.JsonException exception)
                {
                    var message = "Could not deserialize the response body stream as " + typeof(T).FullName + ".";
                    throw new ApiException(message, (int)response.StatusCode, string.Empty, headers, exception);
                }
            }
        }

        private string ConvertToString(object value, System.Globalization.CultureInfo cultureInfo)
        {
            if (value == null)
            {
                return "";
            }

            if (value is System.Enum)
            {
                var name = System.Enum.GetName(value.GetType(), value);
                if (name != null)
                {
                    var field = System.Reflection.IntrospectionExtensions.GetTypeInfo(value.GetType()).GetDeclaredField(name);
                    if (field != null)
                    {
                        var attribute = System.Reflection.CustomAttributeExtensions.GetCustomAttribute(field, typeof(System.Runtime.Serialization.EnumMemberAttribute)) 
                            as System.Runtime.Serialization.EnumMemberAttribute;
                        if (attribute != null)
                        {
                            return attribute.Value != null ? attribute.Value : name;
                        }
                    }

                    var converted = System.Convert.ToString(System.Convert.ChangeType(value, System.Enum.GetUnderlyingType(value.GetType()), cultureInfo));
                    return converted == null ? string.Empty : converted;
                }
            }
            else if (value is bool) 
            {
                return System.Convert.ToString((bool)value, cultureInfo).ToLowerInvariant();
            }
            else if (value is byte[])
            {
                return System.Convert.ToBase64String((byte[]) value);
            }
            else if (value.GetType().IsArray)
            {
                var array = System.Linq.Enumerable.OfType<object>((System.Array) value);
                return string.Join(",", System.Linq.Enumerable.Select(array, o => ConvertToString(o, cultureInfo)));
            }

            var result = System.Convert.ToString(value, cultureInfo);
            return result == null ? "" : result;
        }
    }













public partial class UserLoginDto
{
	[Newtonsoft.Json.JsonProperty("username", Required = Newtonsoft.Json.Required.Always)]
	[System.ComponentModel.DataAnnotations.Required]
	[System.ComponentModel.DataAnnotations.StringLength(int.MaxValue, MinimumLength = 2)]
	public string Username { get; set; }

	[Newtonsoft.Json.JsonProperty("password", Required = Newtonsoft.Json.Required.Always)]
	[System.ComponentModel.DataAnnotations.Required]
	[System.ComponentModel.DataAnnotations.StringLength(int.MaxValue, MinimumLength = 2)]
	public string Password { get; set; }

}

public partial class UserLoginResponseDto
{
	[Newtonsoft.Json.JsonProperty("token", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public string Token { get; set; }

	[Newtonsoft.Json.JsonProperty("expiresIn", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public int ExpiresIn { get; set; }

}

public partial class AccountClient
{
	private System.Net.Http.HttpClient _httpClient;
	private System.Lazy<Newtonsoft.Json.JsonSerializerSettings> _settings;

	public AccountClient(System.Net.Http.HttpClient httpClient)
	{
		_httpClient = httpClient;
		_settings = new System.Lazy<Newtonsoft.Json.JsonSerializerSettings>(CreateSerializerSettings, true);
	}

	private Newtonsoft.Json.JsonSerializerSettings CreateSerializerSettings()
	{
		var settings = new Newtonsoft.Json.JsonSerializerSettings();
		UpdateJsonSerializerSettings(settings);
		return settings;
	}

	protected Newtonsoft.Json.JsonSerializerSettings JsonSerializerSettings { get { return _settings.Value; } }

	partial void UpdateJsonSerializerSettings(Newtonsoft.Json.JsonSerializerSettings settings);

	partial void PrepareRequest(System.Net.Http.HttpClient client, System.Net.Http.HttpRequestMessage request, string url);
	partial void PrepareRequest(System.Net.Http.HttpClient client, System.Net.Http.HttpRequestMessage request, System.Text.StringBuilder urlBuilder);
	partial void ProcessResponse(System.Net.Http.HttpClient client, System.Net.Http.HttpResponseMessage response);

	/// <returns>Success</returns>
	/// <exception cref="ApiException">A server side error occurred.</exception>
	public virtual System.Threading.Tasks.Task<UserLoginResponseDto> LoginAsync(string tenantKey, UserLoginDto body)
	{
		return LoginAsync(tenantKey, body, System.Threading.CancellationToken.None);
	}

	/// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
	/// <returns>Success</returns>
	/// <exception cref="ApiException">A server side error occurred.</exception>
	public virtual async System.Threading.Tasks.Task<UserLoginResponseDto> LoginAsync(string tenantKey, UserLoginDto body, System.Threading.CancellationToken cancellationToken)
	{
		if (tenantKey == null)
			throw new System.ArgumentNullException("tenantKey");

		var urlBuilder_ = new System.Text.StringBuilder();
		urlBuilder_.Append("api/Account/login/{tenantKey}");
		urlBuilder_.Replace("{tenantKey}", System.Uri.EscapeDataString(ConvertToString(tenantKey, System.Globalization.CultureInfo.InvariantCulture)));

		var client_ = _httpClient;
		var disposeClient_ = false;
		try
		{
			using (var request_ = new System.Net.Http.HttpRequestMessage())
			{
				var json_ = Newtonsoft.Json.JsonConvert.SerializeObject(body, _settings.Value);
				var content_ = new System.Net.Http.StringContent(json_);
				content_.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json");
				request_.Content = content_;
				request_.Method = new System.Net.Http.HttpMethod("POST");
				request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("text/plain"));

				PrepareRequest(client_, request_, urlBuilder_);

				var url_ = urlBuilder_.ToString();
				request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);

				PrepareRequest(client_, request_, url_);

				var response_ = await client_.SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
				var disposeResponse_ = true;
				try
				{
					var headers_ = System.Linq.Enumerable.ToDictionary(response_.Headers, h_ => h_.Key, h_ => h_.Value);
					if (response_.Content != null && response_.Content.Headers != null)
					{
						foreach (var item_ in response_.Content.Headers)
							headers_[item_.Key] = item_.Value;
					}

					ProcessResponse(client_, response_);

					var status_ = (int)response_.StatusCode;
					if (status_ == 200)
					{
						var objectResponse_ = await ReadObjectResponseAsync<UserLoginResponseDto>(response_, headers_, cancellationToken).ConfigureAwait(false);
						if (objectResponse_.Object == null)
						{
							throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
						}
						return objectResponse_.Object;
					}
					else
					if (status_ == 401)
					{
						var objectResponse_ = await ReadObjectResponseAsync<string>(response_, headers_, cancellationToken).ConfigureAwait(false);
						if (objectResponse_.Object == null)
						{
							throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
						}
						throw new ApiException<string>("Unauthorized", status_, objectResponse_.Text, headers_, objectResponse_.Object, null);
					}
					else
					{
						var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
						throw new ApiException("The HTTP status code of the response was not expected (" + status_ + ").", status_, responseData_, headers_, null);
					}
				}
				finally
				{
					if (disposeResponse_)
						response_.Dispose();
				}
			}
		}
		finally
		{
			if (disposeClient_)
				client_.Dispose();
		}
	}

	protected struct ObjectResponseResult<T>
	{
		public ObjectResponseResult(T responseObject, string responseText)
		{
			this.Object = responseObject;
			this.Text = responseText;
		}

		public T Object { get; }

		public string Text { get; }
	}

	public bool ReadResponseAsString { get; set; }

	protected virtual async System.Threading.Tasks.Task<ObjectResponseResult<T>> ReadObjectResponseAsync<T>(System.Net.Http.HttpResponseMessage response, System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IEnumerable<string>> headers, System.Threading.CancellationToken cancellationToken)
	{
		if (response == null || response.Content == null)
		{
			return new ObjectResponseResult<T>(default(T), string.Empty);
		}

		if (ReadResponseAsString)
		{
			var responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
			try
			{
				var typedBody = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(responseText, JsonSerializerSettings);
				return new ObjectResponseResult<T>(typedBody, responseText);
			}
			catch (Newtonsoft.Json.JsonException exception)
			{
				var message = "Could not deserialize the response body string as " + typeof(T).FullName + ".";
				throw new ApiException(message, (int)response.StatusCode, responseText, headers, exception);
			}
		}
		else
		{
			try
			{
				using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
				using (var streamReader = new System.IO.StreamReader(responseStream))
				using (var jsonTextReader = new Newtonsoft.Json.JsonTextReader(streamReader))
				{
					var serializer = Newtonsoft.Json.JsonSerializer.Create(JsonSerializerSettings);
					var typedBody = serializer.Deserialize<T>(jsonTextReader);
					return new ObjectResponseResult<T>(typedBody, string.Empty);
				}
			}
			catch (Newtonsoft.Json.JsonException exception)
			{
				var message = "Could not deserialize the response body stream as " + typeof(T).FullName + ".";
				throw new ApiException(message, (int)response.StatusCode, string.Empty, headers, exception);
			}
		}
	}

	private string ConvertToString(object value, System.Globalization.CultureInfo cultureInfo)
	{
		if (value == null)
		{
			return "";
		}

		if (value is System.Enum)
		{
			var name = System.Enum.GetName(value.GetType(), value);
			if (name != null)
			{
				var field = System.Reflection.IntrospectionExtensions.GetTypeInfo(value.GetType()).GetDeclaredField(name);
				if (field != null)
				{
					var attribute = System.Reflection.CustomAttributeExtensions.GetCustomAttribute(field, typeof(System.Runtime.Serialization.EnumMemberAttribute))
						as System.Runtime.Serialization.EnumMemberAttribute;
					if (attribute != null)
					{
						return attribute.Value != null ? attribute.Value : name;
					}
				}

				var converted = System.Convert.ToString(System.Convert.ChangeType(value, System.Enum.GetUnderlyingType(value.GetType()), cultureInfo));
				return converted == null ? string.Empty : converted;
			}
		}
		else if (value is bool)
		{
			return System.Convert.ToString((bool)value, cultureInfo).ToLowerInvariant();
		}
		else if (value is byte[])
		{
			return System.Convert.ToBase64String((byte[])value);
		}
		else if (value.GetType().IsArray)
		{
			var array = System.Linq.Enumerable.OfType<object>((System.Array)value);
			return string.Join(",", System.Linq.Enumerable.Select(array, o => ConvertToString(o, cultureInfo)));
		}

		var result = System.Convert.ToString(value, cultureInfo);
		return result == null ? "" : result;
	}
}

public partial class ProblemDetails
{
	[Newtonsoft.Json.JsonProperty("type", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public string Type { get; set; }

	[Newtonsoft.Json.JsonProperty("title", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public string Title { get; set; }

	[Newtonsoft.Json.JsonProperty("status", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public int? Status { get; set; }

	[Newtonsoft.Json.JsonProperty("detail", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public string Detail { get; set; }

	[Newtonsoft.Json.JsonProperty("instance", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
	public string Instance { get; set; }

	private System.Collections.Generic.IDictionary<string, object> _additionalProperties;

	[Newtonsoft.Json.JsonExtensionData]
	public System.Collections.Generic.IDictionary<string, object> AdditionalProperties
	{
		get { return _additionalProperties ?? (_additionalProperties = new System.Collections.Generic.Dictionary<string, object>()); }
		set { _additionalProperties = value; }
	}

}

public partial class ApiException : System.Exception
{
	public int StatusCode { get; private set; }

	public string Response { get; private set; }

	public System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IEnumerable<string>> Headers { get; private set; }

	public ApiException(string message, int statusCode, string response, System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IEnumerable<string>> headers, System.Exception innerException)
		: base(message + "\n\nStatus: " + statusCode + "\nResponse: \n" + ((response == null) ? "(null)" : response.Substring(0, response.Length >= 512 ? 512 : response.Length)), innerException)
	{
		StatusCode = statusCode;
		Response = response;
		Headers = headers;
	}

	public override string ToString()
	{
		return string.Format("HTTP Response: \n\n{0}\n\n{1}", Response, base.ToString());
	}
}

public partial class ApiException<TResult> : ApiException
{
	public TResult Result { get; private set; }

	public ApiException(string message, int statusCode, string response, System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IEnumerable<string>> headers, TResult result, System.Exception innerException)
		: base(message, statusCode, response, headers, innerException)
	{
		Result = result;
	}
}

// You can define other methods, fields, classes and namespaces here
