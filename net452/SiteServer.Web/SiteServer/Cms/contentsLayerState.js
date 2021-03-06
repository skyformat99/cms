﻿var $url = '/pages/cms/contentsLayerState';

var $data = {
  siteId: parseInt(utils.getQueryString('siteId')),
  channelId: parseInt(utils.getQueryString('channelId')),
  contentId: parseInt(utils.getQueryString('contentId')),
  pageLoad: false,
  pageAlert: null,
  contentChecks: null,
  title: null,
  checkState: null
};

var $methods = {
  loadConfig: function () {
    var $this = this;

    $api.get($url, {
      params: {
        siteId: $this.siteId,
        channelId: $this.channelId,
        contentId: $this.contentId
      }
    }).then(function (response) {
      var res = response.data;

      $this.contentChecks = res.value;
      $this.title = res.title;
      $this.checkState = res.checkState;
    }).catch(function (error) {
      $this.pageAlert = utils.getPageAlert(error);
    }).then(function () {
      $this.pageLoad = true;
    });
  },

  btnSubmitClick: function () {
    window.parent.layer.closeAll();
    window.parent.utils.openLayer({
      title: "审核内容",
      url: "contentsLayerCheck.cshtml?siteId=" +
        this.siteId +
        "&channelId=" +
        this.channelId +
        "&contentIds=" +
        this.contentId,
      full: true
    });
  }
};

new Vue({
  el: '#main',
  data: $data,
  methods: $methods,
  created: function () {
    this.loadConfig();
  }
});