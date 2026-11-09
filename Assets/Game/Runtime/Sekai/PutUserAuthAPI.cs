namespace Sekai
{
	public class PutUserAuthAPI : IAPICaller<object, object>
	{
		private readonly APICore<object, object> _core = new APICore<object, object>();

		public PutUserAuthAPI(string credential, string deviceId, string triggerType, bool forceUpdate)
		{
			// TODO(original): store UserAuthRequest once API DTOs are copied.
		}

		public string Execute(APICore<object, object>.OnAPIEventHandler onCallBackResponse)
		{
			onCallBackResponse?.Invoke(_core);
			return _core.ApiID;
		}

		public string ExecuteFirst(APICore<object, object>.OnAPIEventHandler onCallBackResponse)
		{
			return Execute(onCallBackResponse);
		}

		public void AddCallback(APICore<object, object>.OnAPIEventHandler onCallBackResponse)
		{
			_core.AddCallback(onCallBackResponse);
		}

		public void AddExecuteBehaviour(APIExecuteBehaviourParam param)
		{
			_core.ExecuteBehaviourParam = param;
		}

		public void ClearCallBack()
		{
			_core.ClearCallBack();
		}
	}
}
