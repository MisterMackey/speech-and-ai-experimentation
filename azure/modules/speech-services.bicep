param location string = resourceGroup().location
param speechResourceName string = 'mySpeechResource'
param skuName string = 'S0'
param skuCapacity int = 1

resource speechResource 'Microsoft.CognitiveServices/accounts@2024-04-01-preview' = {
  name: speechResourceName
  location: location
  sku: {
    name: skuName
    capacity: skuCapacity
  }
  kind: 'SpeechServices'
  properties: {
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Deny'
      ipRules: [
        {
          value: '62.163.43.139'
        }
        
      ]
    }
  }
}
