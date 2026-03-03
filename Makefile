.PHONY: default set_npmrc gen_npm_package after_compiled
NPMRC := registry=https://registry.npmmirror.com\\n@chillyroom:registry=http://repo.chilly.room/repository/npm/
# 定义选项列表
OPTIONS := "GMManagementClient" "ApiManagementClient" "退出"
VERSION := 0.0.0
default: select_type
select_type:
	@echo "需要选择生成哪个应用的npm包："
	@select opt in $(OPTIONS); do \
		if [ "$$opt" = "退出" ]; then \
			echo "Exiting..."; \
			exit 0; \
		elif [ -n "$$opt" ]; then \
			echo "已选择: $$opt"; \
			$(MAKE) gen_npm_package SELECTED_OPTION=$$opt; \
			break; \
		else \
			echo "无效选择项，请重新输入"; \
		fi; \
	done
set_npmrc:
	@echo $(NPMRC) > "$$SELECTED_OPTION/.npmrc"
gen_npm_package:
	@if [ -f "$$SELECTED_OPTION/package.json" ]; then \
		$(MAKE) set_npmrc SELECTED_OPTION=$$SELECTED_OPTION; \
		VERSION=$$(jq -r '.version // empty' "$$SELECTED_OPTION/package.json"); \
		SERVICE_NAME=$$(jq -r '.name // empty' "$$SELECTED_OPTION/package.json" | sed -E 's/@chillyroom\/(.*)-management-client/\1/'); \
		PROTO_PATH=$$(jq -r '.protoPath // empty' "$$SELECTED_OPTION/package.json"); \
		SERVICE_FILES=$$(jq -r '.rpcServiceFiles // empty' "$$SELECTED_OPTION/package.json"); \
	else \
		VERSION=$(VERSION); \
	fi && \
	echo "当前版本为: $$VERSION" && \
	read -p "输入发布的版本号: " INPUT_VERSION && \
	VERSION=$${INPUT_VERSION:-$$VERSION} && \
	read -p "输入发布的服务名(最终的包名 @chillyroom/输入值-management-client)[当前值：$$SERVICE_NAME]: " INPUT_SERVICE_NAME && \
	SERVICE_NAME=$${INPUT_SERVICE_NAME:-$$SERVICE_NAME} && \
	read -p "输入.proto文件所在相对目录,[当前值：$$PROTO_PATH]: " INPUT_PROTO_PATH && \
	PROTO_PATH=$${INPUT_PROTO_PATH:-$$PROTO_PATH} && \
	echo "输入要生成服务(内容包含service定义)的.proto文件名,多个文件用|分隔" && \
	echo "示例：S1.proto|S2.proto" && \
	echo "当前值：$$SERVICE_FILES" && \
	read -p "请输入: " INPUT_SERVICE_FILES && \
	SERVICE_FILES=$${INPUT_SERVICE_FILES:-$$SERVICE_FILES} && \
	echo "************************************************" && \
	echo "要发布的版本号为: $$VERSION" && \
	echo ".proto文件所在的路径：$$PROTO_PATH" && \
	echo "服务文件所在的路径：$$SERVICE_FILES" && \
	echo "完整的npm包名：@chillyroom/$$SERVICE_NAME-management-client" && \
	echo "************************************************" && \
	read -p "请仔细阅读上面信息并确认是否继续发布(y/n): " INPUT_COMFIRM && \
	if [ "$$INPUT_COMFIRM" = "y" ]; then \
		YARN_NODE_LINKER=node-modules npx @chillyroom/cli@latest new "$$SELECTED_OPTION" -t node-grpc-client --meta "protobufPath=$$PROTO_PATH,version=$$VERSION,serviceName=$$SERVICE_NAME-management,rpcServiceFiles=$$SERVICE_FILES" && \
		printf "nodeLinker: node-modules\n" > "$$SELECTED_OPTION/.yarnrc.yml" && \
		jq --arg version "$$VERSION" \
		   --arg serviceName "$$SERVICE_NAME" \
		   --arg protoPath "$$PROTO_PATH" \
		   --arg rpcServiceFiles "$$SERVICE_FILES" \
		   '.version = $$version | .name = "@chillyroom/" + $$serviceName + "-management-client" | .protoPath = $$protoPath | .rpcServiceFiles = $$rpcServiceFiles' \
		   "$$SELECTED_OPTION/package.json" > "$$SELECTED_OPTION/tmp.json" && \
		mv "$$SELECTED_OPTION/tmp.json" "$$SELECTED_OPTION/package.json"; \
	fi && \
	$(MAKE) after_compiled SELECTED_OPTION=$$SELECTED_OPTION
after_compiled:
	@rm -rf "$$SELECTED_OPTION/.npmrc"
