load('ext://git_resource', 'git_checkout')

git_checkout('https://gitlab.chilly.room/backend/dev-cluster', '.deploy/dev-cluster', unsafe_mode=True)
load('.deploy/dev-cluster/Tiltfile', 'deploy_infra', 'deploy_common_components', 'get_deployment_yaml', 'run_configuration_sync_by_job', 'run_sql_in_crdb')

deploy_infra()
deploy_common_components()

run_configuration_sync_by_job()

## mingw
config.define_bool('live')
cfg = config.parse()
cwd = os.getcwd()
deploy_path = os.path.join(cwd, 'Cultivating/.deploy')

run_sql_in_crdb('初始化 BuildingGame 数据库', os.path.join(deploy_path, '/Migrations.sql'), labels='BuildingGame')