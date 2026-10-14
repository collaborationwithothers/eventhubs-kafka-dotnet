import gfm from 'remark-gfm';
import validateLinks from 'remark-validate-links';
import repositoryRules from './scripts/docs-rules.mjs';

export default {
  plugins: [gfm, [validateLinks, {repository: false}], repositoryRules],
};
